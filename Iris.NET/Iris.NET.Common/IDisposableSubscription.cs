using System;
using System.Collections.Generic;
using System.Linq;
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
        public string Channel { get; }

        public ContentHandler ContentHandler { get; }

        private IIrisNode _irisNode;

        public IrisDisposableSubscription(IIrisNode irisNode, string channel, ContentHandler contentHandler)
        {
            _irisNode = irisNode;
            Channel = channel;
            ContentHandler = contentHandler;
        }

        public void Dispose()
        {
            _irisNode?.Unsubscribe(Channel, ContentHandler);
        }
    }
}
