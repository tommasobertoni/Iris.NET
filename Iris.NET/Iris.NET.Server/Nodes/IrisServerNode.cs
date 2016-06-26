using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    public class IrisServerNode : AbstractIrisNode<IrisBaseConfig>
    {
        public override bool IsConnected => false;

        private IrisServer _server;

        public IrisServerNode(IrisServer server)
        {
            _server = server;
        }

        protected override void Send(IrisPacket packet)
        {
            throw new NotImplementedException();
        }

        protected override AbstractIrisListener OnConnect(IrisBaseConfig config)
        {
            throw new NotImplementedException();
        }

        protected override void OnMetaReceived(IrisMeta meta)
        {
            throw new NotImplementedException();
        }

        protected override void OnDispose()
        {
            throw new NotImplementedException();
        }
    }
}
