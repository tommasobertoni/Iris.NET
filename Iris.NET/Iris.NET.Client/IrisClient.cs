using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Iris.NET
{
    public class IrisClient : IrisNode
    {
        private TcpClient _socket;
        private Listener _subscriptionsListener;
        private volatile NetworkStream _networkStream;
        private Dictionary<string, LinkedList<MessageHandler>> _channelsSubscriptions = new Dictionary<string, LinkedList<MessageHandler>>();

        public bool IsConnected => _socket?.Connected ?? false;

        public Guid ClientId { get; } = Guid.NewGuid();

        public bool Connect(IrisConfig config)
        {
            if (IsConnected)
                return false;

            _socket = new TcpClient(config.Hostname, config.Port);
            _networkStream = _socket.GetStream();
            _subscriptionsListener = new Listener(_networkStream, config.MessageFailureAttempts);
            _subscriptionsListener.OnMessageReceived += OnMessageReceived;
            _subscriptionsListener.OnErrorReceived += OnErrorReceived;
            _subscriptionsListener.OnInvalidDataReceived += OnInvalidDataReceived;
            _subscriptionsListener.OnException += OnException;
            _subscriptionsListener.Start();

            return true;
        }

        #region PubSub
        public bool SendAsync(string channel, object content, bool propagateThroughHierarchy = false)
        {
            if (!IsConnected)
                return false;

            var message = new IrisMessage(ClientId, channel, propagateThroughHierarchy);
            message.PublicationDateTime = DateTime.Now;
            message.Content = content;
            Send(message);

            return true;
        }

        public bool Subscribe(string channel, MessageHandler messageHandler)
        {
            if (!IsConnected)
                return false;

            lock (_channelsSubscriptions)
            {
                LinkedList<MessageHandler> subs = null;
                if (_channelsSubscriptions.TryGetValue(channel, out subs))
                {
                    subs.AddLast(messageHandler);
                }
                else
                {
                    subs = new LinkedList<MessageHandler>();
                    subs.AddFirst(messageHandler);
                    _channelsSubscriptions.Add(channel, subs);
                }
            }

            var sub = new IrisSubscribe(ClientId, channel);
            Send(sub);
            return true;
        }

        public bool Unsubscribe(string channel, MessageHandler messageHandler)
        {
            if (!IsConnected)
                return false;

            lock (_channelsSubscriptions)
            {
                LinkedList<MessageHandler> subs = null;
                if (_channelsSubscriptions.TryGetValue(channel, out subs))
                {
                    if (subs.Remove(messageHandler))
                    {
                        var unsub = new IrisUnsubscribe(ClientId, channel);
                        Send(unsub);
                        return true;
                    }
                }
            }

            return false;
        }
        #endregion

        #region Messages handling
        private void OnInvalidDataReceived(byte[] data)
        {

        }

        private void OnException(Exception ex)
        {

        }

        private void OnErrorReceived(IrisError error)
        {

        }

        private void OnMessageReceived(IrisMessage error)
        {

        }
        #endregion

        public void Dispose()
        {
            _socket.Close();
            _subscriptionsListener.Stop();
            _subscriptionsListener.OnMessageReceived -= OnMessageReceived;
            _subscriptionsListener.OnErrorReceived -= OnErrorReceived;
            _subscriptionsListener.OnInvalidDataReceived -= OnInvalidDataReceived;
            _subscriptionsListener.OnException -= OnException;
        }

        #region Private
        private void Send(IrisPacket packet)
        {
            var stream = packet.SerializeToMemoryStream();
            var rowData = stream.ToArray();
            _networkStream.Write(rowData, 0, rowData.Length);
        }
        #endregion
    }

    class Listener
    {
        public bool IsListening => _thread != null && _keepListening;

        private Thread _thread;
        private NetworkStream _networkStream;
        private volatile bool _keepListening;
        private int _failureAttempts;

        public Listener(NetworkStream networkStream, int failureAttempts = 1)
        {
            _networkStream = networkStream;
            _failureAttempts = failureAttempts;
        }

        #region Events
        internal delegate void InvalidDataHandler(byte[] data);
        internal event InvalidDataHandler OnInvalidDataReceived;

        internal delegate void ExceptionHandler(Exception ex);
        internal event ExceptionHandler OnException;

        internal delegate void ErrorHandler(IrisError error);
        internal event ErrorHandler OnErrorReceived;

        internal delegate void MessageHandler(IrisMessage error);
        internal event MessageHandler OnMessageReceived;
        #endregion

        public void Start()
        {
            _thread = new Thread(Listen);
            _thread.Start();
            // Loop until worker thread activates.
            while (!_thread.IsAlive);
        }

        private void Listen()
        {
            MemoryStream stream = null;
            while (_keepListening)
            {
                try
                {
                    stream = ReadFully(_networkStream);
                    var data = stream.DeserializeFromMemoryStream();

                    if (data is IrisError)
                        OnErrorReceived.BeginInvoke(data as IrisError, null, null);
                    else
                        OnMessageReceived.BeginInvoke(data as IrisMessage, null, null);
                }
                catch (InvalidCastException)
                {
                    OnInvalidDataReceived.BeginInvoke(stream?.ToArray(), null, null);
                }
                catch (Exception ex)
                {
                    OnException.BeginInvoke(ex, null, null);
                }

                stream = null;
            }
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

        public void Stop()
        {
            _keepListening = false;
            _thread.Join();
            _thread = null;
        }
    }
}
