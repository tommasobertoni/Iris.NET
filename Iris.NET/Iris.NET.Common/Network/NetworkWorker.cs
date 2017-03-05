using Iris.NET.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Iris.NET.Network
{
    /// <summary>
    /// Class used to handle asynchronous writing and reading from a NetworkStream.
    /// </summary>
    public class NetworkWorker
    {
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
        /// Blocking queue used to store the packets yet to be sent.
        /// </summary>
        protected BlockingQueue<AsyncNetworkOperation> _packetsQueue = new BlockingQueue<AsyncNetworkOperation>();

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
        private volatile bool _isAlive;
        /// <summary>
        /// Indicates if this worker is alive and running.
        /// </summary>
        public bool IsAlive
        {
            get { return _isAlive; }
            protected set { _isAlive = value; }
        }

        /// <summary>
        /// Creates a new NetworkWorker using the specific NetworkStream.
        /// </summary>
        /// <param name="networkStream">The NetworkStream to work with.</param>
        public NetworkWorker(NetworkStream networkStream)
        {
            _networkStream = networkStream;
            _writerThread = new Thread(WriteData);
            _listenerThread = new Thread(ListenIncomingData);

            OnConnectionReset += Stop; // Subscribe the Stop method to the connection reset event.
        }

        /// <summary>
        /// Starts the worker and its write/read threads.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start()
        {
            if (!IsAlive)
            {
                IsAlive = true;
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
            IsAlive = false;
            
            try
            {
                _networkStream?.Close();
                _networkStream = null;
            }
            catch { }

            try
            {
                _listenerThread?.Join();
                _listenerThread = null;
            }
            catch { }

            _packetsQueue.Dispose(); // Unlocks the writer thread from the blocking Dequeue operation

            try
            {
                _writerThread?.Join();
                _writerThread = null;
            }
            catch { }
        }

        /// <summary>
        /// Stores the data until asynchronously sending it to the NetworkStream.
        /// </summary>
        /// <param name="data">The object/data to send.</param>
        /// <returns>A task which result indicates whether or not the data has been sent.</returns>
        public Task<bool> SendAsync(object data)
        {
            TaskCompletionSource<bool> asyncOperation = new TaskCompletionSource<bool>();
            _packetsQueue.Enqueue(new AsyncNetworkOperation(data, asyncOperation));
            return asyncOperation.Task;
        }

        #region Private
        private void WriteData()
        {
            while (IsAlive)
            {
                try
                {
                    AsyncNetworkOperation currentOperation = _packetsQueue.Dequeue(); // Dequeue (or wait)

                    if (IsAlive) // Check again if the worker is alive ("dequeue" could have been unlocked during disposing)
                    {
                        object data = currentOperation.Data;

                        var dataMemoryStream = new MemoryStream();
                        data.SerializeToMemoryStream(dataMemoryStream); // Copy data into stream

                        var bytes = dataMemoryStream.ToArray(); // Bytes to send
                        byte[] rawDataLength = BitConverter.GetBytes(bytes.Length); // Serialize the data's length into a byte[] (of length 4)

                        _networkStream?.Write(rawDataLength, 0, rawDataLength.Length); // First send the data's length
                        _networkStream?.Write(bytes, 0, bytes.Length); // Then the actual data

                        currentOperation.SetComplete(); // Set operation complete
                    }
                }
                catch (Exception ex)
                {
                    if (IsAlive)
                        InvokeAsyncOnException(ex);
                }
            }
        }

        private void ListenIncomingData()
        {
            while (IsAlive)
            {
                // Since the data length is always 4 bytes, the buffer used is always the same
                byte[] dataLengthBuffer = new byte[4];

                try
                {
                    // Read the incoming data's lenght (first 4 bytes)
                    ReadExact(dataLengthBuffer, 0, dataLengthBuffer.Length);
                    int dataLength = BitConverter.ToInt32(dataLengthBuffer, 0);

                    // Read the actual incoming data
                    byte[] dataBuffer = new byte[dataLength];
                    ReadExact(dataBuffer, 0, dataBuffer.Length);

                    object data = new MemoryStream(dataBuffer).DeserializeFromMemoryStream();
                    HandleReceivedData(data);
                }
                catch (Exception ex)
                {
                    if (IsAlive)
                    {
                        InvokeAsyncOnException(ex);

                        if (ex is ObjectDisposedException ||
                            ex is EndOfStreamException ||
                            IsSocketResetException(ex))
                        {
                            // Remote socket closed!
                            IsAlive = false;
                            InvokeAsyncOnConnectionReset();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads from the network stream a defined number of bytes and stores them in the given buffer.
        /// (inspired by Marc Gravell at http://blog.marcgravell.com/2013/02/how-many-ways-can-you-mess-up-io.html)
        /// </summary>
        /// <param name="buffer">The array of bytes that will contain the data read from the stream.</param>
        /// <param name="offset">The location in buffer to begin storing the data to (usually 0).</param>
        /// <param name="count">How many bytes to read.</param>
        private void ReadExact(byte[] buffer, int offset, int count)
        {
            int bytesRead;
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            while (count != 0 && (bytesRead = _networkStream.Read(buffer, offset, count)) > 0)
            {
                offset += bytesRead; // Shifts the starting position of the next batch of bytes
                count -= bytesRead; // Decreases the number of bytes to read
            }

            if (count != 0)
                throw new EndOfStreamException();
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
