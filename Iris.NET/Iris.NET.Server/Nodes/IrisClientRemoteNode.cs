using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Server
{
    internal class IrisClientRemoteNode : AbstractIrisNode<IrisServerConfig>
    {
        private IPubSubRouter _pubSubRouter;
        private IrisServerConfig _serverConfig;
        private TcpClient _clientSocket;
        private NetworkStream _networkStream;
        private volatile int _attemptsCount;

        private IrisMeta _defaultACK = new IrisMeta { Request = Request.Resend, ACK = true };

        public IrisClientRemoteNode(TcpClient clientSocket)
        {
            _clientSocket = clientSocket;
        }

        public override bool IsConnected => _pubSubRouter != null && _networkStream != null;

        protected override AbstractIrisListener OnConnect(IrisServerConfig serverConfig)
        {
            if (IsConnected)
                return null;

            _serverConfig = serverConfig;
            _pubSubRouter = _serverConfig.PubSubRouter;
            
            if (_pubSubRouter != null)
            {
                _networkStream = _clientSocket.GetStream();
                _pubSubRouter.Register(this);
                return new IrisServerListener(_networkStream, serverConfig.MessageFailureAttempts);
            }

            return null;
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

        protected override void OnMessageReceived(IUserSubmittedPacket packet)
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
                Send(new IrisMeta { ACK = result.Value });
        }

        protected override void OnInvalidDataReceived(object data)
        {
            Send(new IrisMeta
            {
                Request = Request.Resend,
                ACK = false
            });
        }

        protected override void OnErrorReceived(IrisError error)
        {
            base.OnErrorReceived(error);
        }

        protected override void OnListenerException(Exception ex)
        {
            base.OnListenerException(ex);
        }

        public override void Dispose()
        {
            base.Dispose();
            _networkStream.Close();
            _networkStream = null;
            _clientSocket.Close();
            _clientSocket = null;
            _pubSubRouter = null;
        }
    }
}
