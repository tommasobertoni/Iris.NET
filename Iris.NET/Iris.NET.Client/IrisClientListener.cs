using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Client
{
    internal class IrisClientListener : AbstractNetworkIrisListener
    {
        public IrisClientListener(NetworkStream networkStream)
               : base(networkStream)
        {
        }
    }
}
