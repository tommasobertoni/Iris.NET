using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    public class IrisPacket
    {
        internal IrisPacket(Guid publisherId)
        {
            PacketId = Guid.NewGuid();
            PublisherId = publisherId;
        }

        public readonly Guid PacketId;

        internal Guid PublisherId { get; }
    }
}
