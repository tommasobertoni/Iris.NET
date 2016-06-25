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

        public override void Dispose()
        {
            base.Dispose();
            _networkStream.Close();
            _socket.Close();
        }
    }
}
