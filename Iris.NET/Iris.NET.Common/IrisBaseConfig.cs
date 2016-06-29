using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Base class for node configuration.
    /// </summary>
    public class IrisBaseConfig
    {
        /// <summary>
        /// Number of attempts for sending a packet to the network.
        /// Default value is 2.
        /// </summary>
        public int MessageFailureAttempts { get; set; } = 2;
    }
}
