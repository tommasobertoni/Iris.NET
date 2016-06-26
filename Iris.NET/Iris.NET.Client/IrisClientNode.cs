using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Client
{
    public class IrisClientNode : AbstractIrisNode<IrisClientConfig>
    {
        protected TcpClient _socket;
        protected volatile NetworkStream _networkStream;

        public override bool IsConnected => _socket?.Connected ?? false;

        protected override AbstractIrisListener OnConnect(IrisClientConfig config)
        {
            if (IsConnected)
                return null;
            
            _socket = new TcpClient(config.Hostname, config.Port);
            _networkStream = _socket.GetStream();
            return new IrisClientListener(_networkStream, config.MessageFailureAttempts);
        }

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

        protected override void OnMetaReceived(IrisMeta meta)
        {
        }

        protected override void OnListenerException(Exception ex)
        {
            if (!IsPeerAlive())
                Dispose();

            base.OnListenerException(ex);
        }

        protected override void OnNullReceived()
        {
            if (!IsPeerAlive())
                Dispose();
            else
                _lastException = null;
        }

        protected override void OnDispose()
        {
            _networkStream?.Close();
            _socket?.Close();
        }
    }
}
