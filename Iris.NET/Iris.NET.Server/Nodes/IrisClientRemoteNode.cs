using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Server
{
    /// <summary>
    /// Network client remote node.
    /// Represents the connection to a remote client node.
    /// </summary>
    internal class IrisClientRemoteNode : AbstractIrisNode<IrisServerConfig>, IMessageSubscriber
    {
        #region Properties
        /// <summary>
        /// Indicates if this node is connected.
        /// </summary>
        public override bool IsConnected => _pubSubRouter != null && _networkStream != null;
        #endregion

        private IPubSubRouter _pubSubRouter;
        private IrisServerConfig _serverConfig;
        private TcpClient _clientSocket;
        private NetworkStream _networkStream;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="clientSocket">The tcp socket connection to the remote client.</param>
        public IrisClientRemoteNode(TcpClient clientSocket)
        {
            _clientSocket = clientSocket;
        }

        /// <summary>
        /// Invoked when the node is connecting.
        /// </summary>
        /// <param name="config">The connection's configuration.</param>
        /// <returns>An IrisServerListener instance.</returns>
        protected override AbstractIrisListener OnConnect(IrisServerConfig config)
        {
            if (_clientSocket == null)
                throw new ObjectDisposedException(nameof(IrisClientRemoteNode));

            if (IsConnected)
                return null;

            _serverConfig = config;
            _pubSubRouter = _serverConfig.PubSubRouter;

            _networkStream = _clientSocket.GetStream();
            _pubSubRouter.Register(this);
            return new IrisServerNetworkListener(_networkStream);
        }

        /// <summary>
        /// Sends the packet to the remote client.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        protected override void Send(IrisPacket packet)
        {
            if (packet != null)
            {
                var stream = packet.SerializeToMemoryStream();
                var rowData = stream.ToArray();
                _networkStream.Write(rowData, 0, rowData.Length);
                _networkStream.Flush();
            }
        }

        /// <summary>
        /// Handler for a meta packet received from the network.
        /// </summary>
        /// <param name="meta">The IrisMeta received.</param>
        protected override void OnMetaReceived(IrisMeta meta)
        {
        }

        /// <summary>
        /// Handler for a user submitted packet received from the IrisListener.
        /// If the data is valid, it's given to the IPubSubRouter to be handled.
        /// If the data is valid, it sends an IrisMeta packet with positive ACK.
        /// </summary>
        /// <param name="packet">The IUserSubmittedPacket packet received.</param>
        protected override void OnUserSubmittedPacketReceived(IUserSubmittedPacket packet)
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
                Send(new IrisMeta(NodeId) { ACK = result.Value });
            }
        }

        /// <summary>
        /// Handler for invalid data received from the IrisListener.
        /// Sends an IrisMeta packet with a "Resend" request.
        /// </summary>
        /// <param name="data">The invalid data received.</param>
        protected override void OnInvalidDataReceived(object data)
        {
            Send(new IrisMeta(NodeId)
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
        protected override void OnListenerException(Exception ex)
        {
            if (!IsPeerAlive())
                Dispose();
            else
                _lastException = null;

            base.OnListenerException(ex);
        }

        /// <summary>
        /// Handler for null data received from the IrisListener.
        /// Checks if the peer is alive: if it's not it disposes.
        /// Fires a log event if LogNullsEnable is true.
        /// </summary>
        protected override void OnNullReceived()
        {
            if (!IsPeerAlive())
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
            _networkStream.Close();
            _networkStream = null;
            _clientSocket.Close();
            _clientSocket = null;
            _pubSubRouter = null;
        }

        public void ReceiveMessage(IrisMessage message) => Send(message.TargetChannel, message.Content, message.PropagateThroughHierarchy);
    }
}
