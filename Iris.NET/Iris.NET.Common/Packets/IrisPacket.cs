using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    [Serializable]
    public class IrisPacket
    {
        internal IrisPacket(Guid publisherId)
        {
            PacketId = Guid.NewGuid();
            PublisherId = publisherId;
        }

        public readonly Guid PacketId;

        public Guid PublisherId { get; }
    }
}
