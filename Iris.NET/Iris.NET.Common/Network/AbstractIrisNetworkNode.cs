using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Iris.NET.Network
{
    /// <summary>
    /// Abstract implementation of AbstractIrisNode, targeting network communication.
    /// </summary>
    public abstract class AbstractIrisNetworkNode<T> : AbstractIrisNode<T>
        where T : IrisBaseConfig
    {
        #region Properties
        /// <summary>
        /// Indicates if this node is connected.
        /// </summary>
        public override bool IsConnected => _networkWorker?.IsAlive ?? false;
        #endregion

        private NetworkWorker _networkWorker;

        #region Abstract
        /// <summary>
        /// Invoked when the node is connecting.
        /// </summary>
        /// <returns>The network stream used to communicate to the pubsub network.</returns>
        protected abstract NetworkStream GetNetworkStream();
        #endregion

        /// <summary>
        /// Invoked when the node is connecting.
        /// </summary>
        /// <param name="config">The connection's configuration.</param>
        protected override void OnConnect(T config)
        {
            var networkStream = GetNetworkStream();
            _networkWorker = new NetworkWorker(networkStream);
            HookEventsToWorker();
            _networkWorker?.Start();
        }

        /// <summary>
        /// Invoked when the node is disposing.
        /// </summary>
        protected override void OnDispose()
        {
            _networkWorker?.Stop();
            UnhookEventsFromListener();
        }

        /// <summary>
        /// Attaches handlers to the IrisListener events.
        /// </summary>
        /// <param name="networkWorker">The target IrisListener.</param>
        protected void HookEventsToWorker(NetworkWorker networkWorker = null)
        {
            if (networkWorker == null)
                networkWorker = _networkWorker;

            if (networkWorker != null)
            {
                networkWorker.OnClientSubmittedPacketReceived += OnClientSubmittedPacketReceived;
                networkWorker.OnMetaReceived += OnMetaReceived;
                networkWorker.OnErrorReceived += OnError;
                networkWorker.OnInvalidDataReceived += OnInvalidDataReceived;
                networkWorker.OnException += HandleException;
                networkWorker.OnNullReceived += OnNullReceived;
            }
        }

        /// <summary>
        /// Detaches the handlers from the IrisListener events.
        /// </summary>
        /// <param name="networkWorker">The target IrisListener.</param>
        protected void UnhookEventsFromListener(NetworkWorker networkWorker = null)
        {
            if (networkWorker == null)
                networkWorker = _networkWorker;

            if (networkWorker != null)
            {
                networkWorker.OnClientSubmittedPacketReceived -= OnClientSubmittedPacketReceived;
                networkWorker.OnMetaReceived -= OnMetaReceived;
                networkWorker.OnErrorReceived -= OnError;
                networkWorker.OnInvalidDataReceived -= OnInvalidDataReceived;
                networkWorker.OnException -= HandleException;
                networkWorker.OnNullReceived -= OnNullReceived;
            }
        }

        /// <summary>
        /// Publishes the packet to the network.
        /// </summary>
        /// <param name="packet">The packet to publish.</param>
        protected override Task<bool> Publish(IrisPacket packet) => _networkWorker.SendAsync(packet);
    }
}
