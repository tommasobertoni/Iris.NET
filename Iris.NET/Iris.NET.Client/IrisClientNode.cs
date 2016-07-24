using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Client
{
    /// <summary>
    /// Network client node.
    /// </summary>
    public class IrisClientNode : AbstractIrisNode<IrisClientConfig>
    {
        #region Properties
        /// <summary>
        /// Indicates if this node is connected.
        /// </summary>
        public override bool IsConnected => _socket?.Connected ?? false;
        #endregion

        /// <summary>
        /// TCP client used to communicate with the remote iris network.
        /// </summary>
        protected TcpClient _socket;

        /// <summary>
        /// Network IO stream.
        /// </summary>
        protected volatile NetworkStream _networkStream;

        /// <summary>
        /// Invoked when the node is connecting.
        /// </summary>
        /// <param name="config">The connection's configuration.</param>
        /// <returns>An IrisClientListener instance.</returns>
        protected override AbstractIrisListener OnConnect(IrisClientConfig config)
        {
            if (IsConnected)
                return null;
            
            _socket = new TcpClient(config.Hostname, config.Port);
            _networkStream = _socket.GetStream();
            return new IrisClientListener(_networkStream);
        }

        /// <summary>
        /// Sends the packet to the network.
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
        /// Handler for exceptions coming from the IrisListener.
        /// Disposes the node if the connection is down.
        /// Fires a OnException event.
        /// Fires a log event if LogExceptionsEnable is true.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        protected override void OnListenerException(Exception ex)
        {
            if (!IsPeerAlive())
                Dispose();
            else
                OnListenerException(ex);
        }

        /// <summary>
        /// Handler for null data received from the IrisListener.
        /// Disposes the node if the connection is down.
        /// Fires a log event if LogNullsEnable is true.
        /// </summary>
        protected override void OnNullReceived()
        {
            if (!IsPeerAlive())
                Dispose();
            else
                _lastException = null;
        }

        /// <summary>
        /// Invoked when the node is disposing.
        /// </summary>
        protected override void OnDispose()
        {
            _networkStream?.Close();
            _socket?.Close();
        }
    }
}
