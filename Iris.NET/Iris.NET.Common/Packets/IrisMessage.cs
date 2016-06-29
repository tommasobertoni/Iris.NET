using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Class for pubsub communication
    /// </summary>
    [Serializable]
    public sealed class IrisMessage : IrisPacket, IUserSubmittedPacket
    {
        /// <summary>
        /// Constructor-
        /// </summary>
        /// <param name="publisherId">Guid of the client who sent this packet.</param>
        /// <param name="targetChannel">The channel targeted by this message. If it is "null" the message targets every client (broadcast).</param>
        /// <param name="propagateThroughHierarchy">Indicates if the message also targets all the clients who are subscribed to child channels compared to the target channel.</param>
        internal IrisMessage(Guid publisherId, string targetChannel, bool propagateThroughHierarchy = false)
                            : base(publisherId)
        {
            TargetChannel = targetChannel;
            PropagateThroughHierarchy = propagateThroughHierarchy;
        }

        /// <summary>
        /// The channel targeted by this message. If it is "null" the message targets every client (broadcast).
        /// </summary>
        public string TargetChannel { get; }

        /// <summary>
        /// Indicates if the message also targets all the clients who are subscribed to child channels compared to the target channel.
        /// </summary>
        public bool PropagateThroughHierarchy { get; }

        /// <summary>
        /// Timestamp of publication.
        /// </summary>
        public DateTime PublicationDateTime { get; set; }

        /// <summary>
        /// The content sent by the client.
        /// </summary>
        public object Content { get; set; }
    }
}
