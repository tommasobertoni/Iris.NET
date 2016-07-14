using Iris.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    /// <summary>
    /// Interface for pub/sub routing operations.
    /// </summary>
    public interface IPubSubRouter : IDisposable
    {
        /// <summary>
        /// Registers a node. This is required for a node in order to publish and subscribe.
        /// Allows the node to receive broadcast messages.
        /// </summary>
        /// <param name="node">The node to register.</param>
        /// <returns>Operation succeeded.</returns>
        bool Register(IMessageSubscriber node);

        /// <summary>
        /// Unregisters a node. This also deletes every subscription of that node.
        /// </summary>
        /// <param name="node">The node to unregister.</param>
        /// <returns>Operation succeeded.</returns>
        bool Unregister(IMessageSubscriber node);

        /// <summary>
        /// Submits a message to its channel's subscribers.
        /// </summary>
        /// <param name="sender">The submitter node.</param>
        /// <param name="message">The message to submit.</param>
        /// <returns>Operation succeeded.</returns>
        bool SubmitMessage(IMessageSubscriber sender, IrisMessage message);

        /// <summary>
        /// Subscribes a node to a channel in order to receive target messages.
        /// </summary>
        /// <param name="node">The node to subscribe.</param>
        /// <param name="channel">The channel to which subscribe.</param>
        /// <returns>Operation succeeded.</returns>
        bool Subscribe(IMessageSubscriber node, string channel);

        /// <summary>
        /// Unsubscribes a node from a channel in order to stop receiving target messages.
        /// </summary>
        /// <param name="node">The node to unsubscribe.</param>
        /// <param name="channel">The channel from which unsubscribe.</param>
        /// <returns>Operation succeeded.</returns>
        bool Unsubscribe(IMessageSubscriber node, string channel);
    }
}
