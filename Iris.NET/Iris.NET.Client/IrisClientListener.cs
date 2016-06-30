using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Client
{
    /// <summary>
    /// Network client node listener.
    /// </summary>
    internal class IrisClientListener : AbstractNetworkIrisListener
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="networkStream">The network stream that the node is connected to</param>
        public IrisClientListener(NetworkStream networkStream)
               : base(networkStream)
        {
        }
    }
}
