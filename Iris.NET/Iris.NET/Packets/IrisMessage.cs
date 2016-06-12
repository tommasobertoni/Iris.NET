using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    public sealed class IrisMessage : IrisPacket
    {
        internal IrisMessage(Guid publisherId, string targetChannel, bool propagateThroughHierarchy = false)
                            : base(publisherId)
        {
            TargetChannel = targetChannel;
            PropagateThroughHierarchy = propagateThroughHierarchy;
        }

        public string TargetChannel { get; }

        internal bool PropagateThroughHierarchy { get; }

        public DateTime PublicationDateTime { get; internal set; }

        public object Content { get; set; }
    }
}
