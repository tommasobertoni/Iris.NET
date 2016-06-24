using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    internal interface IrisNode : IDisposable
    {
        bool Subscribe(string channel, ContentHandler messageHandler);

        bool Unsubscribe(string channel, ContentHandler messageHandler);

        //bool Send(string channel, object content, bool propagateThroughHierarchy = false);

        bool SendAsync(string channel, object content, bool propagateThroughHierarchy = false);
    }

    public delegate void ContentHandler(object content);
}
