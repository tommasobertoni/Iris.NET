using Iris.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    public interface IPubSubRouter : IDisposable
    {
        bool Register(IIrisNode node);

        bool Unregister(IIrisNode node);

        bool SubmitMessage(IIrisNode node, IrisMessage message);

        bool Subscribe(IIrisNode node, string channel);

        bool Unsubscribe(IIrisNode node, string channel);
    }
}
