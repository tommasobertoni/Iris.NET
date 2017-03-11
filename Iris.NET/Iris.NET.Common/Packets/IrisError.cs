using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Class for internal/logical error within the library.
    /// </summary>
    [Serializable]
    public class IrisError : IrisPacket
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="publisherId">Guid of the client who sent this packet.</param>
        internal IrisError(Guid publisherId) : base(publisherId) { }

        /// <summary>
        /// The exception that caused this packet.
        /// </summary>
        public Exception Exception { get; internal set; }

        /// <summary>
        /// A log string.
        /// </summary>
        public string Log { get; internal set; }
    }
}
