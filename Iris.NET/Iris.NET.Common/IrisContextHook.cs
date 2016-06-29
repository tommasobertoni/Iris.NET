using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Class used to inform a content handler about the context of the operation
    /// (e.g.: content is null because peer is disconnected or because
    /// the node is unsubscribing from the channel).
    /// </summary>
    public sealed class IrisContextHook
    {
        /// <summary>
        /// Indicates if the node is unsubscribing from the channel of the content handler
        /// </summary>
        public bool Unsubscribing { get; internal set; }
    }
}
