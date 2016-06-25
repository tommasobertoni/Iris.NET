using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    internal class IrisServerConfig : IrisBaseConfig
    {
        public readonly IPubSubRouter PubSubRouter;

        public IrisServerConfig(IPubSubRouter pubSubRouter)
        {
            PubSubRouter = pubSubRouter;
        }
    }
}
