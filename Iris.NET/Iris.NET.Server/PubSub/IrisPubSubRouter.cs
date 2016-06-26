using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    public class IrisPubSubRouter : IPubSubRouter
    {
        private ConcurrentDictionary<IrisNode, List<string>> _nodes = new ConcurrentDictionary<IrisNode, List<string>>();
        private ConcurrentDictionary<string, IrisConcurrentHashSet<IrisNode>> _subscriptions = new ConcurrentDictionary<string, IrisConcurrentHashSet<IrisNode>>();

        public bool Register(IrisNode node)
        {
            if (_nodes.ContainsKey(node))
                return false;

            return _nodes.TryAdd(node, new List<string>());
        }

        public bool Unregister(IrisNode node)
        {
            if (!_nodes.ContainsKey(node))
                return false;

            List<string> channels;
            bool success = _nodes.TryGetValue(node, out channels);

            // Unregister node and remove subscriptions,
            // with removeChannelFromRegisteredNode: false
            // to avoid concurrent modifications over the channels list
            foreach (var channel in channels)
                if (!(success = Unsubscribe(node, channel, removeChannelFromRegisteredNode: false)))
                    break;

            if (success)
                success = _nodes.TryRemove(node, out channels);

            return success;
        }

        public bool SubmitMessage(IrisNode sender, IrisMessage message)
        {
            if (message.Content == null || message.PublisherId == null)
                return false;

            Action<IrisNode> sendMessageToOthersAction = (n) =>
            {
#if !TEST
                if (n != sender)
#endif
                    n.Send(message.TargetChannel, message.Content, message.PropagateThroughHierarchy);
            };

            if (message.TargetChannel == null)
            {
                // Broadcast
                _nodes.Keys.ForEach(sendMessageToOthersAction);
                return true;
            }
            else
            {
                IrisConcurrentHashSet<IrisNode> concurrentHashSet;
                if (_subscriptions.TryGetValue(message.TargetChannel, out concurrentHashSet))
                {
                    concurrentHashSet.ForEach(sendMessageToOthersAction);
                    return true;
                }

                return false;
            }
        }

        public bool Subscribe(IrisNode node, string channel)
        {
            if (!_nodes.ContainsKey(node))
                return false;

            bool success = false;

            if (channel != null)
            {
                IrisConcurrentHashSet<IrisNode> concurrentHashSet;
                if (_subscriptions.TryGetValue(channel, out concurrentHashSet))
                {
                    success = concurrentHashSet.Add(node);
                }
                else
                {
                    concurrentHashSet = new IrisConcurrentHashSet<IrisNode>();
                    concurrentHashSet.Add(node);
                    success = _subscriptions.TryAdd(channel, concurrentHashSet);
                }
            }

            try
            {
                if (success)
                    _nodes[node].Add(channel);
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                success = false;
            }

            return success;
        }

        public bool Unsubscribe(IrisNode node, string channel, bool removeChannelFromRegisteredNode)
        {
            if (!_nodes.ContainsKey(node))
                return false;

            bool success = false;

            if (channel != null)
            {
                IrisConcurrentHashSet<IrisNode> concurrentHashSet;
                if (_subscriptions.TryGetValue(channel, out concurrentHashSet))
                {
                    success = concurrentHashSet.Remove(node);
                }
            }

            if (removeChannelFromRegisteredNode)
            {
                try
                {
                    if (success)
                        _nodes[node].Remove(channel);
                }
                catch (Exception ex) when (!(ex is KeyNotFoundException))
                {
                    success = false;
                }
            }

            return success;
        }

        public bool Unsubscribe(IrisNode node, string channel) => Unsubscribe(node, channel, true);
    }
}
