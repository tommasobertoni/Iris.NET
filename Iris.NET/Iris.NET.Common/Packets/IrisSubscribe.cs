using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Class for requesting to subscribe to a channel.
    /// </summary>
    [Serializable]
    public sealed class IrisSubscribe : IrisPacket
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="publisherId">Guid of the client who sent this packet.</param>
        /// <param name="channel">The channel to which subscribe.</param>
        public IrisSubscribe(Guid publisherId, string channel) : base(publisherId)
        {
            Channel = channel;
            IsClientSubmitted = true;
        }

        /// <summary>
        /// The channel to which subscribe.
        /// </summary>
        public string Channel { get; }
    }
}
