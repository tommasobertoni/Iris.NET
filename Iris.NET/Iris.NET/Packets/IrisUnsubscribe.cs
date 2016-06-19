using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    [Serializable]
    internal class IrisUnsubscribe : IrisSystem
    {
        internal IrisUnsubscribe(Guid publisherId, string channel) : base(publisherId)
        {
            Channel = channel;
        }

        public string Channel { get; }
    }
}
