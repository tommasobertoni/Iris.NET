using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    public interface IMessageSubscriber : IDisposable
    {
        Guid Id { get; }

        void ReceiveMessage(IrisMessage message);
    }
}
