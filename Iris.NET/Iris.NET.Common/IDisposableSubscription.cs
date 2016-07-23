using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Interface for a handler that when disposed unsubscribes the content handler from the channel.
    /// </summary>
    public interface IDisposableSubscription : IDisposable
    {
        /// <summary>
        /// The channel from which unsubscribe.
        /// </summary>
        string Channel { get; }

        /// <summary>
        /// The content handler to unsubscribe.
        /// </summary>
        ContentHandler ContentHandler { get; }
    }

    /// <summary>
    /// An implementation of IDisposableSubscription.
    /// </summary>
    class IrisDisposableSubscription : IDisposableSubscription
    {
        /// <summary>
        /// Indicates if this subscription is already disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// The channel from which unsubscribe.
        /// </summary>
        public string Channel { get; }

        /// <summary>
        /// The content handler to unsubscribe.
        /// </summary>
        public ContentHandler ContentHandler { get; private set; }

        private IIrisNode _irisNode;

        public IrisDisposableSubscription(IIrisNode irisNode, string channel, ContentHandler contentHandler)
        {
            _irisNode = irisNode;
            Channel = channel;
            ContentHandler = contentHandler;
            IsDisposed = false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispose()
        {
            if (!IsDisposed)
            {
                if (Channel == null)
                    _irisNode?.UnsubscribeFromBroadcast(ContentHandler);
                else
                    _irisNode?.Unsubscribe(Channel, ContentHandler);

                _irisNode = null;
                this.ContentHandler = null;
                IsDisposed = true;
            }
        }
    }
}
