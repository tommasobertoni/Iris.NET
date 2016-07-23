using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    /// <summary>
    /// Local client node.
    /// </summary>
    public class IrisServerLocalNode : AbstractIrisNode<IrisServerConfig>, IMessageSubscriber
    {
        private IPubSubRouter _pubSubRouter;

        /// <summary>
        /// Indicates if this node is connected.
        /// </summary>
        private bool _isConnected;
        public override bool IsConnected => _isConnected;

        protected override AbstractIrisListener OnConnect(IrisServerConfig config)
        {
            _pubSubRouter = _config.PubSubRouter;
            _pubSubRouter.Register(this);
            _isConnected = true;
            // Return null because there's no need for a "listener",
            // since the message are received by the invocation of ReceiveMessage.
            return null;
        }

        /// <summary>
        /// Invoked when the node is disposing.
        /// </summary>
        protected override void OnDispose()
        {
            _isConnected = false;
            _pubSubRouter.Unregister(this);
            _pubSubRouter = null;
        }

        /// <summary>
        /// Handler for a meta packet received from the network.
        /// </summary>
        /// <param name="meta">The IrisMeta received.</param>
        protected override void OnMetaReceived(IrisMeta meta)
        {
        }

        /// <summary>
        /// Sends the packet to the network.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        protected override void Send(IrisPacket packet)
        {
            if (packet is IrisSubscribe)
            {
                var subscribeCommand = packet as IrisSubscribe;
                _pubSubRouter.Subscribe(this, subscribeCommand.Channel);
            }
            else if (packet is IrisUnsubscribe)
            {
                var unsubscribeCommand = packet as IrisUnsubscribe;
                _pubSubRouter.Unsubscribe(this, unsubscribeCommand.Channel);
            }
            else
            {
                var message = packet as IrisMessage;
                _pubSubRouter.SubmitMessage(this, message);
            }
        }

        /// <summary>
        /// Invoked when a new message is sent to this subscriber.
        /// </summary>
        /// <param name="message">The message sent.</param>
        public void ReceiveMessage(IrisMessage message) => OnClientSubmittedPacketReceived(message);
    }
}
