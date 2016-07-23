using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Base abstract class for objects sent between clients through the server.
    /// </summary>
    [Serializable]
    public abstract class IrisPacket
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="publisherId">Guid of the client who sent this packet.</param>
        internal IrisPacket(Guid publisherId)
        {
            PacketId = Guid.NewGuid();
            PublisherId = publisherId;
        }

        /// <summary>
        /// Guid of this packet.
        /// </summary>
        public Guid PacketId { get; }

        /// <summary>
        /// Guid of the client who sent this packet.
        /// </summary>
        public Guid PublisherId { get; }

        /// <summary>
        /// Indicates if this packet was sent by a client in response to an api call and it is not an internal communication.
        /// </summary>
        public bool IsClientSubmitted { get; protected set; }
    }
}
