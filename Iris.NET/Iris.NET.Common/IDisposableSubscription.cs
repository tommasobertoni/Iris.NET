using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Iris.NET
{
    public interface IDisposableSubscription : IDisposable
    {
        string Channel { get; }

        ContentHandler ContentHandler { get; }
    }

    class IrisDisposableSubscription : IDisposableSubscription
    {
        public bool IsDisposed { get; private set; }

        public string Channel { get; }

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
