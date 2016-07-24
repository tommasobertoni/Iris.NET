using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Class for meta communication between clients.
    /// #Note This has not a concrete purpose yet... maybe it will be removed
    /// </summary>
    [Serializable]
    public class IrisMeta : IrisPacket
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="publisherId">Guid of the client who sent this packet.</param>
        public IrisMeta(Guid publisherId) : base(publisherId) { }

        /// <summary>
        /// Enum that defines the purpose of this meta packet.
        /// </summary>
        public Request Request { get; set; } = Request.None;

        /// <summary>
        /// Optional: the packet Id that this meta is referring to.
        /// </summary>
        public Guid TargetPacketId { get; set; }

        /// <summary>
        /// Acknowledgement.
        /// </summary>
        public bool? ACK { get; set; }
    }

    /// <summary>
    /// Enum that defines the purpose of meta packets.
    /// </summary>
    public enum Request
    {
        /// <summary>
        /// No request, used for sending a ping.
        /// </summary>
        None,

        /// <summary>
        /// Request to send again the last packet.
        /// </summary>
        Resend,

        /// <summary>
        /// Checks whether or not the receiver is still connected.
        /// </summary>
        AreYouAlive
    }
}
