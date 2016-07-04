using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    /// <summary>
    /// Implementation of IPubSubRouter.
    /// </summary>
    public class IrisPubSubRouter : IPubSubRouter
    {
        private ConcurrentDictionary<IIrisNode, List<string>> _nodes = new ConcurrentDictionary<IIrisNode, List<string>>();
        private ConcurrentDictionary<string, IrisConcurrentHashSet<IIrisNode>> _subscriptions = new ConcurrentDictionary<string, IrisConcurrentHashSet<IIrisNode>>();

        #region Public
        /// <summary>
        /// Registers a node. This is required for a node in order to publish and subscribe.
        /// Allows the node to receive broadcast messages.
        /// </summary>
        /// <param name="node">The node to register.</param>
        /// <returns>Operation succeeded.</returns>
        public bool Register(IIrisNode node)
        {
            if (_nodes.ContainsKey(node))
                return false;

            return _nodes.TryAdd(node, new List<string>());
        }

        /// <summary>
        /// Unregisters a node. This also deletes every subscription of that node.
        /// </summary>
        /// <param name="node">The node to unregister.</param>
        /// <returns>Operation succeeded.</returns>
        public bool Unregister(IIrisNode node)
        {
            if (!_nodes.ContainsKey(node))
                return false;

            List<string> channels;
            bool success = _nodes.TryGetValue(node, out channels);

            // Unregister the node and remove the subscriptions
            // with removeChannelFromRegisteredNode: false
            // to avoid concurrent modifications over the channels list
            foreach (var channel in channels)
                if (!(success = Unsubscribe(node, channel, removeChannelFromRegisteredNode: false)))
                    break;

            if (success)
                success = _nodes.TryRemove(node, out channels);

            return success;
        }

        /// <summary>
        /// Submits a message to its channel's subscribers.
        /// </summary>
        /// <param name="sender">The submitter node.</param>
        /// <param name="message">The message to submit.</param>
        /// <returns>Operation succeeded.</returns>
        public bool SubmitMessage(IIrisNode sender, IrisMessage message)
        {
            if (message.Content == null || message.PublisherId == null || !_nodes.ContainsKey(sender))
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

        /// <summary>
        /// Subscribes a node to a channel in order to receive target messages.
        /// </summary>
        /// <param name="node">The node to subscribe.</param>
        /// <param name="channel">The channel to which subscribe.</param>
        /// <returns>Operation succeeded.</returns>
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

        /// <summary>
        /// Unsubscribes a node from a channel in order to stop receiving target messages.
        /// </summary>
        /// <param name="node">The node to unsubscribe.</param>
        /// <param name="channel">The channel from which unsubscribe.</param>
        /// <returns>Operation succeeded.</returns>
        public bool Unsubscribe(IIrisNode node, string channel) => Unsubscribe(node, channel, true);

        /// <summary>
        /// Disposes the instance and every registered node.
        /// </summary>
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

        /// <summary>
        /// Method used to avoid having concurrent modifications over the subscriptions when the Unregister is invoked.
        /// </summary>
        /// <param name="node">The node to unsubscribe.</param>
        /// <param name="channel">The channel from which unsubscribe.</param>
        /// <param name="removeChannelFromRegisteredNode">If true, removes the channel from the main nodes list.</param>
        /// <returns>Operation succeeded.</returns>
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

            if (removeChannelFromRegisteredNode && success)
            {
                try
                {
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
