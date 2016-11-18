using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
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
        /// Constant size of the "words" sent to the NetworkStream.
        /// A Sentence is composed by 1-n workds (constant-size packets).
        /// </summary>
        protected static readonly int _WordSize = 2 * 1024;

        /// <summary>
        /// Min value of the delay for the loop in the writer thread
        /// </summary>
        protected static readonly double _MinDelayValueMillis = 10;

        /// <summary>
        /// Factor used to increase the delay for the loop in the writer thread
        /// </summary>
        protected static readonly double _IncrementalDelayFactor = 1.02;
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

        /// <summary>
        /// Triggered when the connection was reset by the remote peer.
        /// </summary>
        internal event VoidHandler OnConnectionReset;

        /// <summary>
        /// Delegate for the OnNullReceived and OnConnectionReset events.
        /// </summary>
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

        private double _delay;

        /// <summary>
        /// Creates a new NetworkWorker using the specific NetworkStream.
        /// </summary>
        /// <param name="networkStream">The NetworkStream to work with.</param>
        public NetworkWorker(NetworkStream networkStream)
        {
            _networkStream = networkStream;
            _writerThread = new Thread(DoWork);
            _listenerThread = new Thread(Listen);

            _delay = _MinDelayValueMillis;

            OnConnectionReset += Stop; // Subscribe the Stop method to the connection reset event.
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
            _isAlive = false;

            try
            {
                _networkStream?.Close();
                _networkStream = null;
            }
            catch { }

            try
            {
                _writerThread?.Join();
                _writerThread = null;
            }
            catch { }

            try
            {
                _listenerThread?.Join();
                _listenerThread = null;
            }
            catch { }
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
            MemoryStream sentenceMemoryStream = new MemoryStream(_WordSize);
            byte[] wordBuffer = new byte[_WordSize];

            while (_isAlive)
            {
                try
                {
                    AsyncNetworkOperation currentOperation;
                    if (_packetsQueue.TryDequeue(out currentOperation))
                    {
                        _delay = _MinDelayValueMillis; // Reset delay

                        do
                        {
                            object data = currentOperation.Data;
                            Sentence sentence = new Sentence { Content = data, Void = new byte[0] };
                            sentence.SerializeToMemoryStream(sentenceMemoryStream);

                            long bytesToFill = _WordSize - (sentenceMemoryStream.Length % _WordSize);
                            sentence.Void = new byte[bytesToFill];

                            sentenceMemoryStream.SetLength(0);
                            sentence.SerializeToMemoryStream(sentenceMemoryStream); // Sentence is ready to be sent

                            var rawData = sentenceMemoryStream.ToArray();
                            _networkStream.Write(rawData, 0, rawData.Length);
                            sentenceMemoryStream.SetLength(0);
                            currentOperation.SetComplete();

                        } while (_packetsQueue.TryDequeue(out currentOperation));
                    }
                    else
                    {
                        Thread.Sleep((int)_delay);

                        // No operation found in the queue
                        _delay *= _IncrementalDelayFactor;
                    }
                }
                catch (Exception ex)
                {
                    if (IsAlive)
                        InvokeAsyncOnException(ex);
                }
            }
        }

        private void Listen()
        {
            MemoryStream incomingDataMemoryStream = new MemoryStream();
            MemoryStream currentChunkMemoryStream = new MemoryStream();
            
            while (IsAlive)
            {
                try
                {
                    // Temp stream contains the just-read data from the network
                    var tempStream = _networkStream.ReadNext();
                    tempStream.Seek(0, SeekOrigin.Begin);
                    tempStream.CopyTo(incomingDataMemoryStream); // Copy to the full buffer

                    if (incomingDataMemoryStream.Length >= _WordSize) // If there's enough data for a Sentence
                    {
                        incomingDataMemoryStream.Seek(0, SeekOrigin.Begin); // Reset position

                        while ((incomingDataMemoryStream.Length - incomingDataMemoryStream.Position) >= _WordSize) // While there's enough data for a Sentence
                        {
                            byte[] current = new byte[_WordSize];
                            incomingDataMemoryStream.Read(current, 0, _WordSize); // Copy into current raw data (of constant size)
                            
                            currentChunkMemoryStream.Write(current, 0, _WordSize); // Write raw data to the current chunk memory
                            long currentPosition = currentChunkMemoryStream.Position;

                            try
                            {
                                // TODO: implement a Sentence end key
                                // Try to parse a Sentence
                                currentChunkMemoryStream.Seek(0, SeekOrigin.Begin); // Reset position

                                object obj = currentChunkMemoryStream.DeserializeFromMemoryStream(); // Deserialize into object
                                if (obj is Sentence) // If is a valid Sentence
                                {
                                    var sentence = obj as Sentence;
                                    HandleReceivedData(sentence.Content);
                                }

                                currentChunkMemoryStream.SetLength(0); // Clear the current chunk memory
                            }
                            catch (SerializationException)
                            {
                                currentChunkMemoryStream.Position = currentPosition;
                                // Keep on reading bytes from the stream
                            }
                        }

                        // Reuse temp stream
                        tempStream.SetLength(0); // Clear
                        incomingDataMemoryStream.CopyTo(tempStream);
                        incomingDataMemoryStream = tempStream; // Reference the temp stream that holds the remaining data
                    }
                }
                catch (Exception ex)
                {
                    if (IsAlive)
                    {
                        InvokeAsyncOnException(ex);

                        if (IsSocketResetException(ex))
                        {
                            // Remote socket closed!
                            _isAlive = false;
                            InvokeAsyncOnConnectionReset();
                        }
                    }
                }
            }
        }

        private void HandleReceivedData(object data)
        {
            try
            {
                if (IsAlive)
                {
                    if (data == null)
                        InvokeAsyncOnNullReceived();
                    else if (data is IrisError)
                        InvokeAsyncOnErrorReceived((IrisError)data);
                    else if (data is IrisMeta)
                        InvokeAsyncOnMetaReceived((IrisMeta)data);
                    else
                    {
                        var packet = (IrisPacket)data;
                        if (packet.IsClientSubmitted)
                            InvokeAsyncOnClientSubmittedPacketReceived(packet);
                        else
                            InvokeAsyncOnInvalidDataReceived(data);
                    }
                }
            }
            catch (InvalidCastException)
            {
                if (IsAlive)
                    InvokeAsyncOnInvalidDataReceived(data);
            }
            catch (Exception ex)
            {
                if (IsAlive)
                    InvokeAsyncOnException(ex);
            }
        }

        #region Private
        #region Events invocation
        #region OnInvalidDataReceived
        private void InvokeAsyncOnInvalidDataReceived(object data) => OnInvalidDataReceived?.GetInvocationList().ForEach(e => ((InvalidDataHandler)e).BeginInvoke(data, EndAsyncEventForInvalidDataHandler, EventArgs.Empty));

        private static void EndAsyncEventForInvalidDataHandler(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (InvalidDataHandler)ar.AsyncDelegate;

            try { invokedMethod.EndInvoke(iar); }
            catch { }
        }
        #endregion

        #region OnException
        private void InvokeAsyncOnException(Exception ex) => OnException?.GetInvocationList().ForEach(e => ((ExceptionHandler)e).BeginInvoke(ex, EndAsyncEventForExceptionHandler, EventArgs.Empty));

        private static void EndAsyncEventForExceptionHandler(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (ExceptionHandler)ar.AsyncDelegate;

            try { invokedMethod.EndInvoke(iar); }
            catch { }
        }
        #endregion

        #region OnErrorReceived
        private void InvokeAsyncOnErrorReceived(IrisError error) => OnErrorReceived?.GetInvocationList().ForEach(e => ((ErrorHandler)e).BeginInvoke(error, EndAsyncEventForErrorHandler, EventArgs.Empty));

        private static void EndAsyncEventForErrorHandler(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (ErrorHandler)ar.AsyncDelegate;

            try { invokedMethod.EndInvoke(iar); }
            catch { }
        }
        #endregion

        #region OnClientSubmittedPacketReceived
        private void InvokeAsyncOnClientSubmittedPacketReceived(IrisPacket packet) => OnClientSubmittedPacketReceived?.GetInvocationList().ForEach(e => ((MessageHandler)e).BeginInvoke(packet, EndAsyncEventForMessageHandler, EventArgs.Empty));

        private static void EndAsyncEventForMessageHandler(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (MessageHandler)ar.AsyncDelegate;

            try { invokedMethod.EndInvoke(iar); }
            catch { }
        }
        #endregion

        #region OnMetaReceived
        private void InvokeAsyncOnMetaReceived(IrisMeta meta) => OnMetaReceived?.GetInvocationList().ForEach(e => ((MetaHandler)e).BeginInvoke(meta, EndAsyncEventForMetaHandler, EventArgs.Empty));

        private static void EndAsyncEventForMetaHandler(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (MetaHandler)ar.AsyncDelegate;

            try { invokedMethod.EndInvoke(iar); }
            catch { }
        }
        #endregion

        #region OnNullReceived
        private void InvokeAsyncOnNullReceived() => OnNullReceived?.GetInvocationList().ForEach(e => ((VoidHandler)e).BeginInvoke(EndAsyncEventForVoidHandler, EventArgs.Empty));

        private static void EndAsyncEventForVoidHandler(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (VoidHandler)ar.AsyncDelegate;

            try { invokedMethod.EndInvoke(iar); }
            catch { }
        }
        #endregion

        #region OnConnectionReset
        private void InvokeAsyncOnConnectionReset() => OnConnectionReset?.GetInvocationList().ForEach(e => ((VoidHandler)e).BeginInvoke(EndAsyncEventForVoidHandler, EventArgs.Empty));
        #endregion
        #endregion

        private static bool IsSocketResetException(Exception ex)
        {
            return ex is IOException &&
                   (ex.InnerException as SocketException)?.SocketErrorCode == SocketError.ConnectionReset;
        }
        #endregion

        /// <summary>
        /// Data wrapper used to send a constant-sized object into the NetworkStream.
        /// </summary>
        [Serializable]
        class Sentence
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
