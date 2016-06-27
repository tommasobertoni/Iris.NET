using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    public class IrisPubSubRouter : IPubSubRouter
    {
        private ConcurrentDictionary<IIrisNode, List<string>> _nodes = new ConcurrentDictionary<IIrisNode, List<string>>();
        private ConcurrentDictionary<string, IrisConcurrentHashSet<IIrisNode>> _subscriptions = new ConcurrentDictionary<string, IrisConcurrentHashSet<IIrisNode>>();

        #region Public
        public bool Register(IIrisNode node)
        {
            if (_nodes.ContainsKey(node))
                return false;

            return _nodes.TryAdd(node, new List<string>());
        }

        public bool Unregister(IIrisNode node)
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

        public bool SubmitMessage(IIrisNode sender, IrisMessage message)
        {
            if (message.Content == null || message.PublisherId == null)
                return false;

            Action<IIrisNode> sendMessageToOthersAction = (n) =>
            {
                if (n != sender)
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
                IrisConcurrentHashSet<IIrisNode> concurrentHashSet;
                if (_subscriptions.TryGetValue(message.TargetChannel, out concurrentHashSet))
                {
                    concurrentHashSet.ForEach(sendMessageToOthersAction);
                    return true;
                }

                return false;
            }
        }

        public bool Subscribe(IIrisNode node, string channel)
        {
            if (!_nodes.ContainsKey(node))
                return false;

            bool success = false;

            if (channel != null)
            {
                IrisConcurrentHashSet<IIrisNode> concurrentHashSet;
                if (_subscriptions.TryGetValue(channel, out concurrentHashSet))
                {
                    success = concurrentHashSet.Add(node);
                }
                else
                {
                    concurrentHashSet = new IrisConcurrentHashSet<IIrisNode>();
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

        public bool Unsubscribe(IIrisNode node, string channel) => Unsubscribe(node, channel, true);

        public void Dispose()
        {
            _subscriptions?.Clear();
            _nodes.Keys.ForEach(n =>
            {
                try { n.Dispose(); }
                catch { }
            });
            _nodes?.Clear();
        }
        #endregion

        private bool Unsubscribe(IIrisNode node, string channel, bool removeChannelFromRegisteredNode)
        {
            if (!_nodes.ContainsKey(node))
                return false;

            bool success = false;

            if (channel != null)
            {
                IrisConcurrentHashSet<IIrisNode> concurrentHashSet;
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
    }
}
