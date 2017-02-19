using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Network
{
    public class BrokenConnectionException : Exception
    {
        public BrokenConnectionException()
        {
        }

        public BrokenConnectionException(int consecutiveFailedReadsCount)
            : base($"Consecutive failed reads: {consecutiveFailedReadsCount}")
        {
        }
    }
}
