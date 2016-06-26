using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Iris.NET.Server
{
    public class IrisServer
    {
        #region Properties
        public Guid Id => Guid.NewGuid();
        
        public bool IsRunning => _isRunning;

        public IPAddress Address { get; private set; }

        public int? Port { get; private set; }
        #endregion

        #region Events
        public delegate void ServerExceptionHandler(Exception ex);
        public event ServerExceptionHandler OnServerException;

        public delegate void VoidHandler();
        public event VoidHandler OnStarted;
        public event VoidHandler OnStoped;
        #endregion

        private IPubSubRouter _pubSubRouter;
        private TcpListener _serverSocket;
        private volatile bool _isRunning;
        protected Thread _thread;
        private int _messageFailureAttempts;

        public IrisServer(IPubSubRouter pubSubRouter)
        {
            _pubSubRouter = pubSubRouter;
        }

        #region Public
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(int port, int messageFailureAttempts = 2) => Start(IPAddress.Any, port, messageFailureAttempts);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(IPAddress address, int port, int messageFailureAttempts = 2)
        {
            if (IsRunning)
                return;

            _isRunning = true;
            Address = address;
            Port = port;
            _messageFailureAttempts = messageFailureAttempts;
            _serverSocket = new TcpListener(Address, Port.Value);
            _serverSocket.Start();

            _thread = new Thread(Run);
            _thread.Start();
            // Loop until worker thread activates.
            while (!_thread.IsAlive) ;

            OnStarted?.BeginInvoke(null, null);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (!IsRunning)
                return;

            _isRunning = false;
            Address = null;
            Port = null;
            _serverSocket?.Stop();
            _pubSubRouter.Dispose();
            OnStoped?.BeginInvoke(null, null);
        }
        #endregion

        private void Run()
        {
            try
            {
                while (_isRunning)
                {
                    TcpClient clientSocket = _serverSocket.AcceptTcpClient();
                    var remote = new IrisClientRemoteNode(clientSocket);
                    remote.Connect(new IrisServerConfig(_pubSubRouter)
                    {
                        MessageFailureAttempts = _messageFailureAttempts
                    });
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                    OnServerException?.BeginInvoke(ex, null, null);
            }
            finally
            {
                if (_isRunning)
                    Stop();
            }
        }
    }
}
