using Iris.NET.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Iris.NET.Server
{
    /// <summary>
    /// Network client remote node.
    /// Represents the connection to a remote client node.
    /// </summary>
    internal class IrisClientRemoteNode : AbstractIrisNetworkNode<IrisServerConfig>, IMessageSubscriber
    {
        #region Properties
        /// <summary>
        /// Indicates if this node is connected.
        /// </summary>
        public override bool IsConnected => _pubSubRouter != null && base.IsConnected;
        #endregion

        private IPubSubRouter _pubSubRouter;
        private IrisServerConfig _serverConfig;
        private TcpClient _clientSocket;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="clientSocket">The tcp socket connection to the remote client.</param>
        public IrisClientRemoteNode(TcpClient clientSocket)
        {
            _clientSocket = clientSocket;
        }

        protected override NetworkStream GetNetworkStream() => _clientSocket?.GetStream();

        /// <summary>
        /// Invoked when the node is connecting.
        /// </summary>
        /// <param name="config">The connection's configuration.</param>
        /// <returns>An IrisServerListener instance.</returns>
        protected override void OnConnect(IrisServerConfig config)
        {
            _serverConfig = config;
            _pubSubRouter = _serverConfig.PubSubRouter;
            _pubSubRouter.Register(this);
            base.OnConnect(config);
        }

        /// <summary>
        /// Handler for a packet received from the IrisListener.
        /// If the data is valid, it's given to the IPubSubRouter to be handled.
        /// If the data is valid, it sends an IrisMeta packet with positive ACK.
        /// </summary>
        /// <param name="packet">The packet received.</param>
        protected override void OnClientSubmittedPacketReceived(IrisPacket packet)
        {
            Task.Factory.StartNew(() =>
            {
                bool? result = null;

                if (packet is IrisMessage)
                {
                    result = _pubSubRouter.SubmitMessage(this, packet as IrisMessage);
                }
                else if (packet is IrisSubscribe)
                {
                    result = _pubSubRouter.Subscribe(this, (packet as IrisSubscribe).Channel);
                }
                else if (packet is IrisUnsubscribe)
                {
                    result = _pubSubRouter.Unsubscribe(this, (packet as IrisUnsubscribe).Channel);
                }
                else
                {
                    OnInvalidDataReceived(packet);
                }

                if (result.HasValue)
                {
                    Publish(new IrisMeta(Id) { ACK = result.Value });
                }
            });
        }

        /// <summary>
        /// Handler for invalid data received from the IrisListener.
        /// Sends an IrisMeta packet with a "Resend" request.
        /// </summary>
        /// <param name="data">The invalid data received.</param>
        protected override void OnInvalidDataReceived(object data)
        {
            Publish(new IrisMeta(Id)
            {
                Request = Request.Resend,
                ACK = false
            });
        }

        /// <summary>
        /// Handler for exceptions coming from the IrisListener.
        /// Checks if the peer is alive: if it's not it disposes.
        /// Fires a OnException event.
        /// Fires a log event if LogExceptionsEnable is true.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        protected override async void OnNetworkException(Exception ex)
        {
            if (!await IsPeerAlive())
                Dispose();
            else
                _lastException = null;

            base.OnNetworkException(ex);
        }

        /// <summary>
        /// Handler for null data received from the IrisListener.
        /// Checks if the peer is alive: if it's not it disposes.
        /// Fires a log event if LogNullsEnable is true.
        /// </summary>
        protected override async void OnNullReceived()
        {
            if (!await IsPeerAlive())
                Dispose();

            base.OnNullReceived();
        }

        /// <summary>
        /// Invoked when the node is disposing.
        /// Closes all network streams and unregister this node from the IPubSubRouter instance.
        /// </summary>
        protected override void OnDispose()
        {
            _pubSubRouter.Unregister(this);
            _clientSocket.Close();
            _clientSocket = null;
            _pubSubRouter = null;
        }

        public void ReceiveMessage(IrisMessage message)
        {
            if (message.TargetChannel == null)
                PublishToBroadcast(message.Content);
            else
                Publish(message.TargetChannel, message.Content, message.PropagateThroughHierarchy);
        }
    }
}
