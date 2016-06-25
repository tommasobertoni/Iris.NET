using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Client
{
    public class IrisClientConfig : IrisBaseConfig
    {
        public string Hostname { get; set; }

        public int Port { get; set; }
    }
}
