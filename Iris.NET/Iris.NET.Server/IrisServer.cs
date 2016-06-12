using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET
{
    public class IrisServer : IrisNode
    {
        public bool Send(object content)
        {
            throw new NotImplementedException();
        }

        public bool Send(string channel, object content, bool propagateThroughHierarchy = false)
        {
            throw new NotImplementedException();
        }

        public bool Subscribe(string channel, MessageHandler messageHandler)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
