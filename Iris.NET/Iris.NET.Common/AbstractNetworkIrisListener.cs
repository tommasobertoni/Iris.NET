using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;

namespace Iris.NET
{
    public class AbstractNetworkIrisListener : AbstractIrisListener
    {
        private int _messageFailureAttempts;
        private NetworkStream _networkStream;
        private MemoryStream _memoryStream;

        public AbstractNetworkIrisListener(NetworkStream networkStream, int messageFailureAttempts)
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
            object @object = null;
            _memoryStream = _networkStream.ReadNext();

            if (_memoryStream.Length > 0)
                @object = _memoryStream.DeserializeFromMemoryStream();

            return @object;
        }

        protected override void OnStop()
        {
            _networkStream.Close();
        }
    }
}
