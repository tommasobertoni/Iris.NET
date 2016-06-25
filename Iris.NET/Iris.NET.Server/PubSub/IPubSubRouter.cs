using Iris.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    public interface IPubSubRouter
    {
        bool Register(IrisNode node);

        bool Unregister(IrisNode node);

        bool SubmitMessage(IrisNode node, IrisMessage message);

        bool Subscribe(IrisNode node, string channel);

        bool Unsubscribe(IrisNode node, string channel);
    }
}
