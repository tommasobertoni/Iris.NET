using Iris.NET.Collections;
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
        private ConcurrentDictionary<IMessageSubscriber, List<string>> _nodes = new ConcurrentDictionary<IMessageSubscriber, List<string>>();
        private IChannelsSubscriptionsDictionary<IMessageSubscriber> _subsDictionary;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="subsDictionary">An implementation of IChannelsSubscriptionsDictionary. If not specified, it will use an instance of IrisChannelsSubscriptionsDictionary.</param>
        public IrisPubSubRouter(IChannelsSubscriptionsDictionary<IMessageSubscriber> subsDictionary = null)
        {
            _subsDictionary = subsDictionary ?? new IrisChannelsSubscriptionsDictionary<IMessageSubscriber>();
        }

        #region Public
        /// <summary>
        /// Registers a node. This is required for a node in order to publish and subscribe.
        /// Allows the node to receive broadcast messages.
        /// </summary>
        /// <param name="node">The node to register.</param>
        /// <returns>True if the operation succeeded.</returns>
        public bool Register(IMessageSubscriber node)
        {
            if (_nodes.ContainsKey(node))
                return false;

            return _nodes.TryAdd(node, new List<string>());
        }

        /// <summary>
        /// Unregisters a node. This also deletes every subscription of that node.
        /// </summary>
        /// <param name="node">The node to unregister.</param>
        /// <returns>True if the operation succeeded.</returns>
        public bool Unregister(IMessageSubscriber node)
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
        /// <returns>True if the operation succeeded.</returns>
        public bool SubmitMessage(IMessageSubscriber sender, IrisMessage message)
        {
            if (message.PublisherId == null || !_nodes.ContainsKey(sender))
                return false;

            IrisConcurrentHashSet<Guid> _deliveryNodes = new IrisConcurrentHashSet<Guid>();
            Action<IMessageSubscriber> sendMessageToOthersAction = (n) =>
            {
                if (n != sender && !_deliveryNodes.Contains(n.Id))
                {
                    _deliveryNodes.Add(n.Id);
                    n.ReceiveMessage(message);
                }
            };

            if (message.TargetChannel == null)
            {
                // Broadcast
                _nodes.Keys.ForEach(sendMessageToOthersAction);
                return true;
            }
            else
            {
                var irisNodes = _subsDictionary.GetSubscriptions(message.TargetChannel, message.PropagateThroughHierarchy);
                if (irisNodes != null)
                    irisNodes.ForEach(sendMessageToOthersAction);

                return irisNodes != null;
            }
        }

        /// <summary>
        /// Subscribes a node to a channel in order to receive target messages.
        /// </summary>
        /// <param name="node">The node to subscribe.</param>
        /// <param name="channel">The channel to which subscribe.</param>
        /// <returns>True if the operation succeeded.</returns>
        public bool Subscribe(IMessageSubscriber node, string channel)
        {
            if (!_nodes.ContainsKey(node))
                return false;

            bool success = false;

            if (channel != null)
                success = _subsDictionary.Add(node, channel);

            try
            {
                if (success)
                    _nodes[node].Add(channel);
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                _subsDictionary.Remove(node, channel);
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Unsubscribes a node from a channel in order to stop receiving target messages.
        /// </summary>
        /// <param name="node">The node to unsubscribe.</param>
        /// <param name="channel">The channel from which unsubscribe.</param>
        /// <returns>True if the operation succeeded.</returns>
        public bool Unsubscribe(IMessageSubscriber node, string channel) => Unsubscribe(node, channel, true);

        /// <summary>
        /// Disposes the instance and every registered node.
        /// </summary>
        public void Dispose()
        {
            _subsDictionary?.Clear();
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
        /// <returns>True if the operation succeeded.</returns>
        private bool Unsubscribe(IMessageSubscriber node, string channel, bool removeChannelFromRegisteredNode)
        {
            if (!_nodes.ContainsKey(node))
                return false;

            bool success = false;

            if (channel != null)
                success = _subsDictionary.Remove(node, channel);

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
