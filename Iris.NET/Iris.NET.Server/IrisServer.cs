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
    /// <summary>
    /// Network IrisServer.
    /// </summary>
    public class IrisServer
    {
        #region Properties
        /// <summary>
        /// Guid of this server.
        /// </summary>
        public Guid Id => Guid.NewGuid();

        /// <summary>
        /// Indicates if this server is running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// The address on which this server is currently running.
        /// </summary>
        public IPAddress Address { get; private set; }

        /// <summary>
        /// The port on which this server is currently running.
        /// </summary>
        public int? Port { get; private set; }
        #endregion

        #region Events
        public event ServerExceptionHandler OnServerException;
        public delegate void ServerExceptionHandler(Exception ex);

        public event VoidHandler OnStarted;
        public event VoidHandler OnStoped;
        public delegate void VoidHandler();
        #endregion

        private IPubSubRouter _pubSubRouter;
        private TcpListener _serverSocket;
        private volatile bool _isRunning;
        protected Thread _thread;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="pubSubRouter">An implementation of IPubSubRouter. If not specified, it will use an instance of IrisPubSubRouter.</param>
        public IrisServer(IPubSubRouter pubSubRouter = null)
        {
            _pubSubRouter = pubSubRouter ?? new IrisPubSubRouter();
        }

        #region Public
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(int port) => Start(IPAddress.Any, port);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(IPAddress address, int port)
        {
            if (IsRunning)
                return;

            _isRunning = true;
            Address = address;
            Port = port;
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
                    remote.Connect(new IrisServerConfig(_pubSubRouter));
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
