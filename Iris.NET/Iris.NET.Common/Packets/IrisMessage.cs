using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    [Serializable]
    public sealed class IrisMessage : IrisPacket, IUserSubmittedPacket
    {
        public IrisMessage(Guid publisherId, string targetChannel, bool propagateThroughHierarchy = false)
                            : base(publisherId)
        {
            TargetChannel = targetChannel;
            PropagateThroughHierarchy = propagateThroughHierarchy;
        }

        public string TargetChannel { get; }

        public bool PropagateThroughHierarchy { get; }

        public DateTime PublicationDateTime { get; internal set; }

        public object Content { get; set; }
    }
}
