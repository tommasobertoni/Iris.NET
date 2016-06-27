using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Base interface for a network node
    /// </summary>
    public interface IIrisNode : IDisposable
    {
        /// <summary>
        /// Guid of this node
        /// </summary>
        Guid NodeId { get; }

        /// <summary>
        /// Submits the content to the pubsub network
        /// </summary>
        /// <param name="targetChannel">The channel targeted by the content. If it is "null" the content targets every client (broadcast)</param>
        /// <param name="content">The content to send</param>
        /// <param name="propagateThroughHierarchy">Indicates if the content also targets all the clients who are subscribed to child channels compared to the target channel</param>
        /// <returns>Operation succeeded</returns>
        bool Send(string targetChannel, object content, bool propagateThroughHierarchy = false);

        /// <summary>
        /// Subscribes this node to a channel
        /// </summary>
        /// <param name="channel">The channel to which subscribe</param>
        /// <param name="messageHandler">A handler for the content received through this subscription</param>
        /// <returns>Operation succeeded</returns>
        bool Subscribe(string channel, ContentHandler messageHandler);

        /// <summary>
        /// Unsubscribes this node from a channel. All the content handlers subscribed to this channel will be lost.
        /// </summary>
        /// <param name="channel">The channel from which unsubscribe</param>
        /// <returns>Operation succeeded</returns>
        bool Unsubscribe(string channel);
    }

    /// <summary>
    /// Delegate definition for a content handler
    /// </summary>
    /// <param name="content">The content received</param>
    /// <param name="k">Iris hook for other information</param>
    public delegate void ContentHandler(object content, IrisHook k);
}
