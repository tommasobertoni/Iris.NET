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
        /// Size of the information sent that describes the length of the actual data.
        /// </summary>
        protected const int _DataLengthSize = 58;

        /// <summary>
        /// Min value of the delay for the loop in the writer thread
        /// </summary>
        protected const double _MinDelayValueMillis = 10;

        /// <summary>
        /// Factor used to increase the delay for the loop in the writer thread
        /// </summary>
        protected const double _IncrementalDelayFactor = 1.02;
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

        #region Private
        private void DoWork()
        {
            MemoryStream dataLengthMemoryStream = new MemoryStream(_DataLengthSize);
            MemoryStream dataMemoryStream = new MemoryStream(_DataLengthSize);

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
                            dataLengthMemoryStream.SetLength(0); // Clear
                            dataMemoryStream.SetLength(0); // Clear

                            object data = currentOperation.Data;
                            data.SerializeToMemoryStream(dataMemoryStream); // Copy data into stream

                            var dataLength = (int)dataMemoryStream.Length; // Read data length
                            dataLength.SerializeToMemoryStream(dataLengthMemoryStream);
                            dataLengthMemoryStream.SetLength(_DataLengthSize); // Set constant-size data length chunk

                            var rawDataLength = dataLengthMemoryStream.ToArray(); // Send data length
                            _networkStream.Write(rawDataLength, 0, rawDataLength.Length);

                            var rawData = dataMemoryStream.ToArray(); // Send data
                            _networkStream.Write(rawData, 0, rawData.Length);
                            
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
            MemoryStream incomingDataMemoryStream = new MemoryStream(_DataLengthSize);

            byte[] dataBuffer = null;
            bool reset = false; // Indicates if the data has been read and the values must be reset to default
            bool isData = false; // Indicates if it's expecting to read data or data length
            int targetBytes = _DataLengthSize;
            int bytesToReadNow;
            int bytesRead = 0; // Bytes that were read in the previous cycle

            while (IsAlive)
            {
                try
                {
                    // If in the previous cycle some bytes were read, but not all the expected ones
                    bytesToReadNow = targetBytes - bytesRead; // Calculate the remaining bytes to reach the expected value
                    
                    if (bytesRead == 0) // If it's at default, initialize a new buffer
                        dataBuffer = new byte[bytesToReadNow];
                    // else: the values haven't been reset to default, hence the dataBuffer contains partial data bytes

                    bytesRead = _networkStream.Read(dataBuffer, bytesRead, bytesToReadNow);

                    if (bytesRead == bytesToReadNow) // If the expected amount of bytes was read
                    {
                        reset = true; // Reset the values before the next cycle
                        incomingDataMemoryStream.Write(dataBuffer, 0, dataBuffer.Length); // Write the bytes into the stream
                        incomingDataMemoryStream.Position = 0; // Set the position to the begin of the stream

                        object data = incomingDataMemoryStream.DeserializeFromMemoryStream();

                        if (isData)
                        {
                            HandleReceivedData(data);
                            targetBytes = _DataLengthSize;
                        }
                        else
                        {
                            // Is data length
                            targetBytes = (int)data;
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleListenCycleException(ex);

                    bytesRead = 0;
                    incomingDataMemoryStream.SetLength(0);
                }
                finally
                {
                    if (reset)
                    {
                        isData ^= true;
                        bytesRead = 0;
                        incomingDataMemoryStream.SetLength(0);
                        reset = false;
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

        private void HandleListenCycleException(Exception ex)
        {
            if (IsAlive)
            {
                InvokeAsyncOnException(ex);

                if (IsStreamDisposedException(ex) || IsSocketResetException(ex))
                {
                    // Remote socket closed!
                    _isAlive = false;
                    InvokeAsyncOnConnectionReset();
                }
            }
        }

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

        private static bool IsStreamDisposedException(Exception ex) => ex is ObjectDisposedException;

        private static bool IsSocketResetException(Exception ex)
        {
            return ex is IOException &&
                   (ex.InnerException as SocketException)?.SocketErrorCode == SocketError.ConnectionReset;
        }
        #endregion

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
