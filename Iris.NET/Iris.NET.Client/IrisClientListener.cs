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
            _memoryStream = ReadFully(_networkStream);
            return _memoryStream.DeserializeFromMemoryStream();
        }

        /// <summary>
        /// source: http://stackoverflow.com/questions/221925/creating-a-byte-array-from-a-stream#answer-221941
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static MemoryStream ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms;
            }
        }
    }
}
