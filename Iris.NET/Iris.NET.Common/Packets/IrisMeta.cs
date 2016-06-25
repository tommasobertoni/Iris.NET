using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET
{
    public class IrisMeta : IrisPacket
    {
        public Request Request { get; set; } = Request.None;

        public bool ACK { get; set; }
    }

    public enum Request
    {
        None,
        Resend
    }
}
