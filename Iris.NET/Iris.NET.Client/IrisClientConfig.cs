using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Client
{
    /// <summary>
    /// Client node configuration.
    /// </summary>
    public class IrisClientConfig : IrisBaseConfig
    {
        /// <summary>
        /// Server's hostname.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Server's port.
        /// </summary>
        public int Port { get; set; }
    }
}
