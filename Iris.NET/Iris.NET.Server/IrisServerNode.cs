using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Server
{
    public class IrisServerNode : AbstractIrisNode<IrisConfig>
    {
        public override bool IsConnected => false;

        private IrisServer _server;

        public IrisServerNode(IrisServer server)
        {
            _server = server;
        }

        public override bool Connect(IrisConfig config)
        {
            throw new NotImplementedException();
        }

        protected override void Send(IrisPacket packet)
        {
            throw new NotImplementedException();
        }
    }
}
