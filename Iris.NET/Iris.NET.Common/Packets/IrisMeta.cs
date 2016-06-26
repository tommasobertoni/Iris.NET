using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET
{
    [Serializable]
    public class IrisMeta : IrisPacket
    {
        public IrisMeta(Guid publisherId) : base(publisherId) { }

        public Request Request { get; set; } = Request.None;

        public Guid TargetPacketId { get; set; }

        public bool? ACK { get; set; }
    }

    public enum Request
    {
        None,
        Resend,
        AreYouAlive
    }
}
