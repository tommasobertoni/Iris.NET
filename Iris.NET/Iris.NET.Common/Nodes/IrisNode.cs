using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    public interface IrisNode : IDisposable
    {
        Guid NodeId { get; }

        bool Send(string channel, object content, bool propagateThroughHierarchy = false);

        bool Subscribe(string channel, ContentHandler messageHandler);

        bool Unsubscribe(string channel, ContentHandler messageHandler);
    }

    public delegate void ContentHandler(object content);
}
