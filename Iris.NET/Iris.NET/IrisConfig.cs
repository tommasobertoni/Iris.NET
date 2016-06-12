using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    public sealed class IrisConfig
    {
        public string Hostname { get; set; }

        public int Port { get; set; }

        public int MessageFailureAttempts { get; set; } = 2;
    }
}
