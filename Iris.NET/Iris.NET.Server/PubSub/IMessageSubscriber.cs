using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    /// <summary>
    /// Interface used by IPubSubRouter to interact with message subscribers.
    /// </summary>
    public interface IMessageSubscriber : IDisposable
    {
        /// <summary>
        /// Id of this message subscriber.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Invoked when a new message is sent to this subscriber.
        /// </summary>
        /// <param name="message">The message sent.</param>
        void ReceiveMessage(IrisMessage message);
    }
}
