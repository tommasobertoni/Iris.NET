using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Server
{
    public class IrisServer
    {
        public Guid Id => Guid.NewGuid();

        private volatile bool _isRunning;
        public bool IsRunning => _isRunning;

        public IPAddress Address { get; private set; }

        public int? Port { get; private set; }

        private IPubSubRouter _pubSubRouter;
        private TcpListener _serverSocket;

        public IrisServer(IPubSubRouter pubSubRouter)
        {
            _pubSubRouter = pubSubRouter;
        }

        public void Start(int port, int messageFailureAttempts = 2) => Start(IPAddress.Any, port, messageFailureAttempts);

        public void Start(IPAddress address, int port, int messageFailureAttempts = 2)
        {
            if (IsRunning)
                return;

            try
            {
                _isRunning = true;
                Address = address;
                Port = port;
                _serverSocket = new TcpListener(Address, Port.Value);
                _serverSocket.Start();
                OnServerStart?.BeginInvoke(this, null, null, null);

                while (_isRunning)
                {
                    TcpClient clientSocket = _serverSocket.AcceptTcpClient();
                    var remote = new IrisClientRemoteNode(clientSocket);
                    remote.Connect(new IrisServerConfig(_pubSubRouter)
                    {
                        MessageFailureAttempts = messageFailureAttempts
                    });
                }
            }
            catch (Exception ex)
            {
                OnServerException?.BeginInvoke(ex, null, null);
            }
            finally
            {
                Stop();
            }
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            Address = null;
            Port = null;
            _serverSocket.Stop();
            OnServerStop?.BeginInvoke(this, null, null, null);
        }

        #region Events
        public delegate void ServerExceptionHandler(Exception ex);
        public event ServerExceptionHandler OnServerException;

        public event EventHandler OnServerStart;
        public event EventHandler OnServerStop;
        #endregion
    }
}
