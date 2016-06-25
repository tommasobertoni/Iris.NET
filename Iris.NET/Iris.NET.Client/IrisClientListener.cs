using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Iris.NET.Client
{
    internal class IrisClientListener : AbstractIrisListener
    {
        private int messageFailureAttempts;
        private NetworkStream _networkStream;
        private MemoryStream _memoryStream;

        public IrisClientListener(NetworkStream _networkStream, int messageFailureAttempts)
        {
            this._networkStream = _networkStream;
            this.messageFailureAttempts = messageFailureAttempts;
        }

        protected override void InitListenCycle()
        {
            _memoryStream = null;
        }

        protected override object ReadObject()
        {
            _memoryStream = Read(_networkStream);
            return _memoryStream.DeserializeFromMemoryStream();
        }

        private static MemoryStream Read(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            MemoryStream ms = new MemoryStream();
            int read = input.Read(buffer, 0, buffer.Length);
            ms.Write(buffer, 0, read);
            return ms;
        }
    }
}
