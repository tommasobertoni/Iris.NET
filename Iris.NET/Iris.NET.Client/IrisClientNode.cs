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

        public override bool Connect(IrisClientConfig config)
        {
            if (IsConnected)
                return false;

            _socket = new TcpClient(config.Hostname, config.Port);
            _networkStream = _socket.GetStream();
            _subscriptionsListener = new IrisClientListener(_networkStream, config.MessageFailureAttempts);
            HookEventsToListener();
            _subscriptionsListener.Start();

            return true;
        }

        protected override void Send(IrisPacket packet)
        {
            var stream = packet.SerializeToMemoryStream();
            var rowData = stream.ToArray();
            _networkStream.Write(rowData, 0, rowData.Length);
        }

        public override void Dispose()
        {
            base.Dispose();
            _socket.Close();
            _subscriptionsListener.Stop();
        }
    }
}
