using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    internal interface IrisNode : IDisposable
    {
        bool Subscribe(string channel, MessageHandler messageHandler);

        bool Unsubscribe(string channel, MessageHandler messageHandler);

        //bool Send(string channel, object content, bool propagateThroughHierarchy = false);

        bool SendAsync(string channel, object content, bool propagateThroughHierarchy = false);
    }

    public delegate void MessageHandler(IrisMessage message);
}
