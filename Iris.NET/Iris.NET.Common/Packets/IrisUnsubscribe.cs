using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Class for requesting to unsubscribe from a channel
    /// </summary>
    [Serializable]
    public class IrisUnsubscribe : IrisPacket, IUserSubmittedPacket
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="publisherId">Guid of the client who sent this packet</param>
        /// <param name="channel">The channel from which unsubscribe</param>
        public IrisUnsubscribe(Guid publisherId, string channel) : base(publisherId)
        {
            Channel = channel;
        }

        /// <summary>
        /// The channel from which unsubscribe
        /// </summary>
        public string Channel { get; }
    }
}
