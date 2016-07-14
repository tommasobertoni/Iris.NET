using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    /// <summary>
    /// Server node configuration.
    /// </summary>
    public class IrisServerConfig : IrisBaseConfig
    {
        public IPubSubRouter PubSubRouter { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="pubSubRouter">An implementation of IPubSubRouter.</param>
        public IrisServerConfig(IPubSubRouter pubSubRouter)
        {
            PubSubRouter = pubSubRouter;
        }
    }
}
