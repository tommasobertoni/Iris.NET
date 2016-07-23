using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;

namespace Iris.NET
{
    /// <summary>
    /// Abstract implementation of AbstractIrisListener targeting network communications
    /// </summary>
    public class AbstractNetworkIrisListener : AbstractIrisListener
    {
        protected NetworkStream _networkStream;
        protected MemoryStream _memoryStream;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="networkStream">The network stream that the node is connected to</param>
        public AbstractNetworkIrisListener(NetworkStream networkStream)
        {
            _networkStream = networkStream;
        }

        /// <summary>
        /// Initialize the listening cycle.
        /// </summary>
        protected override void InitListenCycle()
        {
            _memoryStream = null;
        }

        /// <summary>
        /// Reads the incoming data and return it as object.
        /// </summary>
        /// <returns>The data as object</returns>
        protected override object ReadObject()
        {
            object obj = null;
            _memoryStream = _networkStream.ReadNext();

            if (_memoryStream.Length > 0)
                obj = _memoryStream.DeserializeFromMemoryStream();

            return obj;
        }

        /// <summary>
        /// Invoked when this listener is stopping.
        /// </summary>
        protected override void OnStop()
        {
            _networkStream.Close();
        }
    }
}
