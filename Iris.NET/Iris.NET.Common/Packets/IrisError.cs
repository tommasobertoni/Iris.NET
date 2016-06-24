using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    [Serializable]
    public class IrisError : IrisPacket
    {
        internal IrisError(Guid publisherId) : base(publisherId) { }

        public bool ByException { get; set; }

        public Exception Exception { get; }
    }
}
