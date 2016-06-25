using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Server
{
    internal class IrisServerListener : AbstractIrisListener
    {
        private int _messageFailureAttempts;
        private NetworkStream _networkStream;
        private MemoryStream _memoryStream;

        public IrisServerListener(NetworkStream networkStream, int messageFailureAttempts)
        {
            _networkStream = networkStream;
            _messageFailureAttempts = messageFailureAttempts;
        }

        protected override void InitListenCycle()
        {
            _memoryStream = null;
        }

        protected override object ReadObject()
        {
            _memoryStream = _networkStream.ReadNext();
            return _memoryStream.DeserializeFromMemoryStream();
        }
    }
}
