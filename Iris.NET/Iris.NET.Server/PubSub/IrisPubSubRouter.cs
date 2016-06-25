using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    public class IrisPubSubRouter : IPubSubRouter
    {
        private IrisConcurrentHashSet<IrisNode> _nodes = new IrisConcurrentHashSet<IrisNode>();
        private ConcurrentDictionary<string, IrisConcurrentHashSet<IrisNode>> _subscriptions = new ConcurrentDictionary<string, IrisConcurrentHashSet<IrisNode>>();

        public bool Register(IrisNode node)
        {
            if (_nodes.Contains(node))
                return false;

            var result = _nodes.Add(node);
            if (result)
                Subscribe(node, null);

            return result;
        }

        public bool Unregister(IrisNode node)
        {
            if (_nodes.Contains(node))
                return false;

            var result = _nodes.Remove(node);
            if (result)
                Unsubscribe(node, null);

            return result;
        }

        public bool SubmitMessage(IrisNode node, IrisMessage message)
        {
            if (message.Content == null || message.PublisherId == null)
                return false;

            Action<IrisNode> sendMessageToOthersAction = (n) =>
            {
                if (n != node)
                    n.Send(message.TargetChannel, message.Content, message.PropagateThroughHierarchy);
            };

            if (message.TargetChannel == null)
            {
                _nodes.ForEach(sendMessageToOthersAction);
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
            if (!_nodes.Contains(node))
                return false;

            if (channel != null)
            {
                IrisConcurrentHashSet<IrisNode> concurrentHashSet;
                if (_subscriptions.TryGetValue(channel, out concurrentHashSet))
                {
                    concurrentHashSet.Add(node);
                    return true;
                }
                else
                {
                    concurrentHashSet = new IrisConcurrentHashSet<IrisNode>();
                    concurrentHashSet.Add(node);
                    return _subscriptions.TryAdd(channel, concurrentHashSet);
                }
            }

            return true;
        }

        public bool Unsubscribe(IrisNode node, string channel)
        {
            if (!_nodes.Contains(node))
                return false;

            if (channel != null)
            {
                IrisConcurrentHashSet<IrisNode> concurrentHashSet;
                if (_subscriptions.TryGetValue(channel, out concurrentHashSet))
                {
                    concurrentHashSet.Add(node);
                    return true;
                }
                else
                {
                    concurrentHashSet = new IrisConcurrentHashSet<IrisNode>();
                    concurrentHashSet.Add(node);
                    return _subscriptions.TryAdd(channel, concurrentHashSet);
                }
            }

            return true;
        }
    }
}
