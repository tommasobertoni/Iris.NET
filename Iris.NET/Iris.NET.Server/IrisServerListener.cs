using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Server
{
    internal class IrisServerListener : AbstractNetworkIrisListener
    {
        public IrisServerListener(NetworkStream networkStream)
               : base(networkStream)
        {
        }
    }
}
