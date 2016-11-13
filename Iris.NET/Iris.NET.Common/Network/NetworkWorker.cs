using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iris.NET
{
    /// <summary>
    /// Class used to handle asynchronous writing and reading from a NetworkStream.
    /// </summary>
    public class NetworkWorker
    {
        #region Static
        /// <summary>
        /// Constant size of the Sentences sent to the NetworkStream.
        /// </summary>
        protected static readonly int _sentenceChunksSize = 5 * 1024;
        #endregion

        #region Events
        /// <summary>
        /// Triggered when the data received could not be deserialized.
        /// </summary>
        internal event InvalidDataHandler OnInvalidDataReceived;
        internal delegate void InvalidDataHandler(object data);

        /// <summary>
        /// Triggered when an exception occurs.
        /// </summary>
        internal event ExceptionHandler OnException;
        internal delegate void ExceptionHandler(Exception ex);

        /// <summary>
        /// Triggered when an IrisError is received.
        /// </summary>
        internal event ErrorHandler OnErrorReceived;
        internal delegate void ErrorHandler(IrisError error);

        /// <summary>
        /// Triggered when a user submitted packet is received.
        /// </summary>
        internal event MessageHandler OnClientSubmittedPacketReceived;
        internal delegate void MessageHandler(IrisPacket packet);

        /// <summary>
        /// Triggered when an IrisMeta packet is received.
        /// </summary>
        internal event MetaHandler OnMetaReceived;
        internal delegate void MetaHandler(IrisMeta meta);

        /// <summary>
        /// Triggered when null data is received.
        /// </summary>
        internal event VoidHandler OnNullReceived;
        internal delegate void VoidHandler();
        #endregion

        /// <summary>
        /// Concurrent queue used to store the packets yet to be sent.
        /// </summary>
        protected ConcurrentQueue<AsyncNetworkOperation> _packetsQueue = new ConcurrentQueue<AsyncNetworkOperation>();

        /// <summary>
        /// Thread dedicated to write to the NetworkStream.
        /// </summary>
        protected Thread _writerThread;

        /// <summary>
        /// Thread dedicated to read from the NetworkStream.
        /// </summary>
        protected Thread _listenerThread;

        /// <summary>
        /// The NetworkStream.
        /// </summary>
        protected NetworkStream _networkStream;

        /// <summary>
        /// Volatile variable used to indicate if this worker is alive and running.
        /// </summary>
        protected volatile bool _isAlive;
        /// <summary>
        /// Indicates if this worker is alive and running.
        /// </summary>
        public bool IsAlive => _isAlive;

        /// <summary>
        /// Creates a new NetworkWorker using the specific NetworkStream.
        /// </summary>
        /// <param name="networkStream">The NetworkStream to work with.</param>
        public NetworkWorker(NetworkStream networkStream)
        {
            _networkStream = networkStream;
            _writerThread = new Thread(DoWork);
            _listenerThread = new Thread(Listen);
        }

        /// <summary>
        /// Starts the worker and its write/read threads.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            if (!_isAlive)
            {
                _isAlive = true;
                _writerThread.Start();
                _listenerThread.Start();

                while (!_writerThread.IsAlive);
                while (!_listenerThread.IsAlive);
            }
        }

        /// <summary>
        /// Stops the server and its write/read threads, and closes the NetworkStream.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Stop()
        {
            if (_isAlive)
            {
                try
                {
                    _networkStream.Close();
                    _networkStream = null;
                }
                catch { }

                try
                {
                    _writerThread.Join();
                    _writerThread = null;
                }
                catch { }

                try
                {
                    _listenerThread.Join();
                    _listenerThread = null;
                }
                catch { }

                _isAlive = false;
            }
        }

        /// <summary>
        /// Stores the data until asynchronously sending it to the NetworkStream.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public Task<bool> SendAsync(object data)
        {
            TaskCompletionSource<bool> asyncOperation = new TaskCompletionSource<bool>();
            _packetsQueue.Enqueue(new AsyncNetworkOperation(data, asyncOperation));            
            return asyncOperation.Task;
        }

        private void DoWork()
        {
            MemoryStream sentenceMemoryStream = new MemoryStream(_sentenceChunksSize);
            byte[] buffer = new byte[_sentenceChunksSize];

            // TODO: incremental delay
            while (_isAlive)
            {
                AsyncNetworkOperation currentOperation;
                while (_packetsQueue.TryDequeue(out currentOperation))
                {
                    object data = currentOperation.Data;
                    Sentence sentence = new Sentence { Content = data, Void = new byte[0] };
                    sentence.SerializeToMemoryStream(sentenceMemoryStream);

                    long bytesToFill = _sentenceChunksSize - (sentenceMemoryStream.Length % _sentenceChunksSize);
                    sentence.Void = new byte[bytesToFill];

                    sentence.SerializeToMemoryStream(sentenceMemoryStream);
                    var rawData = sentenceMemoryStream.ToArray();
                    sentenceMemoryStream.SetLength(0);
                    sentenceMemoryStream.Seek(0, SeekOrigin.Begin);

                    _networkStream.Write(rawData, 0, rawData.Length);

                    currentOperation.SetComplete();
                }
            }
        }

        private void Listen()
        {
            MemoryStream incomingDataMemoryStream = new MemoryStream();
            MemoryStream bufferMemoryStream = new MemoryStream();
            MemoryStream currentChunkMemoryStream = new MemoryStream();

            // TODO: incremental delay
            while (_isAlive)
            {
                // Temp stream contains the just-read data from the network
                var tempStream = _networkStream.ReadNext();
                tempStream.Seek(0, SeekOrigin.Begin);
                tempStream.CopyTo(incomingDataMemoryStream); // Copy to the full buffer

                while (incomingDataMemoryStream.Length >= _sentenceChunksSize) // If there's enough data for a Sentence
                {
                    currentChunkMemoryStream.SetLength(0); // Clear the current chunk memory
                    currentChunkMemoryStream.Seek(0, SeekOrigin.Begin);

                    incomingDataMemoryStream.Seek(0, SeekOrigin.Begin); // Position to the begin
                    byte[] current = new byte[_sentenceChunksSize];
                    incomingDataMemoryStream.Read(current, 0, _sentenceChunksSize); // Copy into current raw data

                    long bufferLength = incomingDataMemoryStream.Length - _sentenceChunksSize; // Length of the remaining data in the full buffer
                    incomingDataMemoryStream.Position = _sentenceChunksSize; // Shift the position to the end of the current data
                    incomingDataMemoryStream.CopyTo(bufferMemoryStream); // Copy to buffer memory
                    bufferMemoryStream.Seek(0, SeekOrigin.Begin); // Position to the begin
                    incomingDataMemoryStream.SetLength(0); // Clear
                    bufferMemoryStream.CopyTo(incomingDataMemoryStream); // Copy back to full buffer
                    bufferMemoryStream.SetLength(0); // Clear

                    currentChunkMemoryStream.Write(current, 0, _sentenceChunksSize); // Write row data to the current chunk memory
                    object obj = currentChunkMemoryStream.DeserializeFromMemoryStream();
                    if (obj is Sentence)
                    {
                        var sentence = obj as Sentence;
                        HandleReceivedData(sentence.Content);
                    }
                }
            }
        }

        private void HandleReceivedData(object data)
        {
            //Task.Factory.StartNew(() =>
            //{
                try
                {
                    if (_isAlive)
                    {
                        if (data == null)
                            OnNullReceived?.BeginInvoke(null, null);
                        else if (data is IrisError)
                            OnErrorReceived?.BeginInvoke((IrisError)data, null, null);
                        else if (data is IrisMeta)
                            OnMetaReceived?.BeginInvoke((IrisMeta)data, null, null);
                        else
                        {
                            var packet = (IrisPacket)data;
                            if (packet.IsClientSubmitted)
                                OnClientSubmittedPacketReceived?.BeginInvoke(packet, null, null);
                            else
                                OnInvalidDataReceived?.BeginInvoke(data, null, null);
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    OnInvalidDataReceived?.BeginInvoke(data, null, null);
                }
                catch (Exception ex)
                {
                    OnException?.BeginInvoke(ex, null, null);
                }
            //});
        }

        /// <summary>
        /// Data wrapper used to send a constant-sized object into the NetworkStream.
        /// </summary>
        [Serializable]
        protected class Sentence
        {
            /// <summary>
            /// The content.
            /// </summary>
            public object Content { get; set; }

            /// <summary>
            /// Void data, used to reach the target sentence-size.
            /// </summary>
            public byte[] Void { get; set; }
        }

        /// <summary>
        /// Wrapper for the data and its asynchronous operation.
        /// </summary>
        protected class AsyncNetworkOperation
        {
            /// <summary>
            /// The data.
            /// </summary>
            public object Data { get; }

            private TaskCompletionSource<bool> _asyncOperation;

            /// <summary>
            /// Creates a new AsyncNetworkOperation wrapper for the data provided.
            /// </summary>
            /// <param name="data">The data.</param>
            /// <param name="asyncOperation">TaskCompletionSource for the asynchronous operation.</param>
            public AsyncNetworkOperation(object data, TaskCompletionSource<bool> asyncOperation)
            {
                Data = data;
                _asyncOperation = asyncOperation;
            }

            /// <summary>
            /// Resolved the TaskCompletionSource provided for the asynchronous operation.
            /// </summary>
            public virtual void SetComplete()
            {
                _asyncOperation.SetResult(true);
            }
        }
    }
}
