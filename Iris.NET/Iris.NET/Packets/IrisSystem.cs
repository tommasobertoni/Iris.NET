using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    [Serializable]
    public class IrisSystem : IrisPacket
    {
        internal IrisSystem(Guid publisherId) : base(publisherId) { }
    }
}
