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
        #region Properties
        public override bool IsConnected => _pubSubRouter != null && _networkStream != null;

        //public bool IsDisposed => _networkStream == null && _clientSocket == null && _pubSubRouter == null;
        #endregion

        private IPubSubRouter _pubSubRouter;
        private IrisServerConfig _serverConfig;
        private TcpClient _clientSocket;
        private NetworkStream _networkStream;
        private volatile int _attemptsCount;

        public IrisClientRemoteNode(TcpClient clientSocket)
        {
            _clientSocket = clientSocket;
        }

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
            return new IrisServerListener(_networkStream, config.MessageFailureAttempts);
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
                if (result.Value)
                    _attemptsCount = 0;
            }
        }

        protected override void OnInvalidDataReceived(object data)
        {
            if (++_attemptsCount < _config.MessageFailureAttempts)
            {
                Send(new IrisMeta(NodeId)
                {
                    Request = Request.Resend,
                    ACK = false
                });
            }
        }

        protected override void OnErrorReceived(IrisError error)
        {
            base.OnErrorReceived(error);
        }

        protected override void OnListenerException(Exception ex)
        {
            if (!IsPeerAlive())
                Dispose();
            else
                _lastException = null;

            base.OnListenerException(ex);
        }

        protected override void OnNullReceived()
        {
            if (!IsPeerAlive())
                Dispose();
        }

        protected override void OnDispose()
        {
            _pubSubRouter.Unregister(this);
            _networkStream.Close();
            _networkStream = null;
            _clientSocket.Close();
            _clientSocket = null;
            _pubSubRouter = null;
        }
    }
}
