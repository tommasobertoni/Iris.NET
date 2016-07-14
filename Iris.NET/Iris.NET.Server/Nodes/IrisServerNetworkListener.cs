using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Server
{
    /// <summary>
    /// Network server node listener.
    /// </summary>
    internal class IrisServerNetworkListener : AbstractNetworkIrisListener
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="networkStream">The network stream that the node is connected to</param>
        public IrisServerNetworkListener(NetworkStream networkStream)
               : base(networkStream)
        {
        }
    }
}