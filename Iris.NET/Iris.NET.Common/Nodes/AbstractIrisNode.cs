using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Iris.NET
{
    /// <summary>
    /// Abstract implementation of IIrisNode, with the addition of events and connection methods.
    /// Uses a AbstractIrisListener to listen for incoming packets from the network and handles channels subscriptions.
    /// It also provides a method for testing if the connection is working properly: IsPeerAlive().
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class AbstractIrisNode<T> : IIrisNode
           where T : IrisBaseConfig
    {
        #region Properties
        /// <summary>
        /// Guid of this node
        /// </summary>
        public Guid NodeId { get; } = Guid.NewGuid();

        /// <summary>
        /// Triggers a log event when a message is received
        /// </summary>
        public bool LogMessagesEnable { get; set; }

        /// <summary>
        /// Triggers a log event when an exception occurs
        /// </summary>
        public bool LogExceptionsEnable { get; set; } = true;

        /// <summary>
        /// Triggers a log event when an error is received
        /// </summary>
        public bool LogErrorsEnable { get; set; } = true;

        /// <summary>
        /// Triggers a log event when invalid data is received
        /// </summary>
        public bool LogInvalidDataEnable { get; set; } = true;

        /// <summary>
        /// Triggers a log event when null is received
        /// </summary>
        public bool LogNullsEnable { get; set; }
        #endregion

        #region Events
        /// <summary>
        /// Triggered when an exception occurs
        /// </summary>
        public event ExceptionHandler OnException;
        public delegate void ExceptionHandler(Exception ex);

        /// <summary>
        /// Triggered when an operation generates a log
        /// </summary>
        public event LogHandler OnLog;
        public delegate void LogHandler(string log);

        /// <summary>
        /// Triggered when the connection succeeded
        /// </summary>
        public event VoidHandler OnConnected;

        /// <summary>
        /// Triggered when the client is disposed
        /// </summary>
        public event VoidHandler OnDisposed;
        public delegate void VoidHandler();
        #endregion
        
        private AbstractIrisListener _subscriptionsListener;
        protected volatile Dictionary<string, LinkedList<ContentHandler>> _channelsSubscriptions = new Dictionary<string, LinkedList<ContentHandler>>();
        protected bool _isDisposing;
        protected Exception _lastException;
        protected T _config;

        #region Abstract
        /// <summary>
        /// Indicates if this node is connected
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        /// Invoked when the node is connecting
        /// </summary>
        /// <param name="config">The connection's configuration</param>
        /// <returns>An AbstractIrisListener</returns>
        protected abstract AbstractIrisListener OnConnect(T config);

        /// <summary>
        /// Invoked when the node is disposing
        /// </summary>
        protected abstract void OnDispose();
        #endregion

        #region Public
        /// <summary>
        /// Connects the node to it's network
        /// </summary>
        /// <param name="config">The connection's configuration</param>
        /// <returns>Operation succeeded</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Connect(T config)
        {
            _config = config;
            _subscriptionsListener = OnConnect(_config);
            HookEventsToListener();
            _subscriptionsListener?.Start();

            var success = _subscriptionsListener != null;
            if (success)
                OnConnected?.BeginInvoke(null, null);

            return success;
        }

        /// <summary>
        /// Closes the connection and disposes all the connection resources
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual void Dispose()
        {
            if (!_isDisposing)
            {
                _isDisposing = true;
                UnhookEventsFromListener();
                _subscriptionsListener?.Stop();
                OnDispose();
                OnDisposed?.BeginInvoke(null, null);

#if TEST
                Console.WriteLine($"{this.GetType().Name}:{this.NodeId} STOPPED");
#endif
            }
        }

        #region PubSub
        /// <summary>
        /// Submits the content to the pubsub network
        /// </summary>
        /// <param name="targetChannel">The channel targeted by the content. If it is "null" the content targets every client (broadcast)</param>
        /// <param name="content">The content to send</param>
        /// <param name="propagateThroughHierarchy">Indicates if the content also targets all the clients who are subscribed to child channels compared to the target channel</param>
        /// <returns>Operation succeeded</returns>
        public virtual bool Send(string targetChannel, object content, bool propagateThroughHierarchy = false)
        {
            if (!IsConnected)
                return false;

            var message = new IrisMessage(NodeId, targetChannel, propagateThroughHierarchy);
            message.PublicationDateTime = DateTime.Now;
            message.Content = content;
            Send(message);

            return true;
        }

        /// <summary>
        /// Subscribes this node to a channel
        /// </summary>
        /// <param name="channel">The channel to which subscribe</param>
        /// <param name="messageHandler">A handler for the content received through this subscription</param>
        /// <returns>Operation succeeded</returns>
        public virtual bool Subscribe(string channel, ContentHandler messageHandler)
        {
            if (!IsConnected)
                return false;

            lock (_channelsSubscriptions)
            {
                LinkedList<ContentHandler> subs = null;
                if (_channelsSubscriptions.TryGetValue(channel, out subs))
                {
                    subs.AddLast(messageHandler);
                }
                else
                {
                    subs = new LinkedList<ContentHandler>();
                    subs.AddFirst(messageHandler);
                    _channelsSubscriptions.Add(channel, subs);
                }
            }

            var sub = new IrisSubscribe(NodeId, channel);
            Send(sub);
            return true;
        }

        /// <summary>
        /// Unsubscribes this node from a channel. All the content handlers subscribed to this channel will be lost.
        /// </summary>
        /// <param name="channel">The channel from which unsubscribe</param>
        /// <returns>Operation succeeded</returns>
        public virtual bool Unsubscribe(string channel)
        {
            if (!IsConnected)
                return false;

            lock (_channelsSubscriptions)
            {
                LinkedList<ContentHandler> subs = null;
                if (_channelsSubscriptions.TryGetValue(channel, out subs))
                {
                    subs.ForEach(s => s.BeginInvoke(null, new IrisContextHook { Unsubscribing = true }, null, null));
                    subs.Clear();
                    var unsub = new IrisUnsubscribe(NodeId, channel);
                    Send(unsub);
                    return true;
                }
            }

            return false;
        }
        #endregion
        #endregion

        /// <summary>
        /// Attaches handlers to the IrisListener events
        /// </summary>
        /// <param name="subscriptionsListener">The target IrisListener</param>
        protected void HookEventsToListener(AbstractIrisListener subscriptionsListener = null)
        {
            if (subscriptionsListener == null)
                subscriptionsListener = _subscriptionsListener;
            
            if (subscriptionsListener != null)
            {
                subscriptionsListener.OnUserSubmittedPacketReceived += OnUserSubmittedPacketReceived;
                subscriptionsListener.OnMetaReceived += OnMetaReceived;
                subscriptionsListener.OnErrorReceived += OnErrorReceived;
                subscriptionsListener.OnInvalidDataReceived += OnInvalidDataReceived;
                subscriptionsListener.OnException += HandleListenerException;
                subscriptionsListener.OnNullReceived += OnNullReceived;
            }
        }

        /// <summary>
        /// Detaches the handlers from the IrisListener events
        /// </summary>
        /// <param name="subscriptionsListener">The target IrisListener</param>
        protected void UnhookEventsFromListener(AbstractIrisListener subscriptionsListener = null)
        {
            if (subscriptionsListener == null)
                subscriptionsListener = _subscriptionsListener;

            if (subscriptionsListener != null)
            {
                subscriptionsListener.OnUserSubmittedPacketReceived -= OnUserSubmittedPacketReceived;
                subscriptionsListener.OnMetaReceived -= OnMetaReceived;
                subscriptionsListener.OnErrorReceived -= OnErrorReceived;
                subscriptionsListener.OnInvalidDataReceived -= OnInvalidDataReceived;
                subscriptionsListener.OnException -= HandleListenerException;
                subscriptionsListener.OnNullReceived -= OnNullReceived;
            }
        }

        /// <summary>
        /// Checks if the connection is working properly by sending a meta packet
        /// </summary>
        /// <returns></returns>
        protected bool IsPeerAlive()
        {
            try
            {
                Send(new IrisMeta(NodeId)
                {
                    ACK = null,
                    Request = Request.AreYouAlive
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        #region Messages handling
        /// <summary>
        /// Builds a string for logging the invalid data received
        /// </summary>
        /// <param name="data">The invalid data received</param>
        /// <returns>A log string</returns>
        protected string GetLogForInvalidDataReceived(object data)
        {
            string runtimeType;
            try
            {
                runtimeType = data.GetType().FullName;
            }
            catch
            {
                runtimeType = "could't retrive";
            }
            return $"[InvalidDataReceived];{nameof(runtimeType)}: {runtimeType}";
        }

        /// <summary>
        /// Handler for invalid data received from the IrisListener
        /// Fires a log event if LogInvalidDataEnable is true
        /// </summary>
        /// <param name="data">The invalid data received</param>
        protected virtual void OnInvalidDataReceived(object data)
        {
            if (LogInvalidDataEnable)
                OnLog?.BeginInvoke(GetLogForInvalidDataReceived(data), null, null);
        }

        /// <summary>
        /// Builds a string for logging the exception that occurred
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        /// <returns>A log string</returns>
        protected string GetLogForException(Exception ex) => $"[Exception];{ex.GetFullException()}";

        /// <summary>
        /// Handler for exceptions coming from the IrisListener
        /// Checks wether this exception was already caught right before and invokes the method OnListenerException
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        private void HandleListenerException(Exception ex)
        {
            if (_lastException == null || ex.Message != _lastException.Message)
            {
                _lastException = ex;
                OnListenerException(ex);
            }
        }

        /// <summary>
        /// Handler for exceptions coming from the IrisListener
        /// Fires a OnException event
        /// Fires a log event if LogExceptionsEnable is true
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        protected virtual void OnListenerException(Exception ex)
        {
            OnException?.BeginInvoke(ex, null, null);
            if (LogExceptionsEnable)
                OnLog?.BeginInvoke(GetLogForException(ex), null, null);
        }

        /// <summary>
        /// Builds a string for logging the error received
        /// </summary>
        /// <param name="error">The IrisError received</param>
        /// <returns>A log string</returns>
        protected string GetLogForErrorReceived(IrisError error) => $"[Error];{nameof(error.PublisherId)}: {error.PublisherId};{nameof(error.Exception)}: {error.Exception?.GetFullException()}";

        /// <summary>
        /// Handler for an error received from the IrisListener
        /// Fires a log event if LogErrorsEnable is true
        /// </summary>
        /// <param name="error">The IrisError received</param>
        protected virtual void OnErrorReceived(IrisError error)
        {
            if (LogErrorsEnable)
                OnLog?.BeginInvoke(GetLogForErrorReceived(error), null, null);
        }

        /// <summary>
        /// Builds a string for logging the message received
        /// </summary>
        /// <param name="message">The IrisMessage received</param>
        /// <returns>A log string</returns>
        protected virtual string GetLogForMessageReceived(IrisMessage message) => $"[Message];{nameof(message.TargetChannel)}: {message.TargetChannel};{nameof(message.PublicationDateTime)}: {message.PublicationDateTime};{nameof(message.PropagateThroughHierarchy)}: {message.PropagateThroughHierarchy}";

        /// <summary>
        /// Handler for a user submitted packet received from the IrisListener
        /// If the packet is of IrisMessage type, invokes the ContentHandlers subscribed to the reception of the message
        /// Fires a log event if LogMessagesEnable is true and the packet is of IrisMessage type
        /// </summary>
        /// <param name="packet">The IUserSubmittedPacket packet received</param>
        protected virtual void OnUserSubmittedPacketReceived(IUserSubmittedPacket packet)
        {
            if (packet is IrisMessage)
            {
                var message = packet as IrisMessage;
                LinkedList<ContentHandler> subscriptions;
                if (_channelsSubscriptions.TryGetValue(message.TargetChannel, out subscriptions))
                {
                    foreach (var subscription in subscriptions)
                        subscription.BeginInvoke(message.Content, null, null, null);
                }

                if (LogMessagesEnable)
                    OnLog?.BeginInvoke(GetLogForMessageReceived(message), null, null);
            }
        }

        /// <summary>
        /// Builds a string for logging null data received
        /// </summary>
        /// <returns>A log string</returns>
        protected virtual string GetLogForNullReceived() => $"[NullReceived] Node: {NodeId}";

        /// <summary>
        /// Handler for null data received from the IrisListener
        /// Fires a log event if LogNullsEnable is true
        /// </summary>
        protected virtual void OnNullReceived()
        {
            if (LogNullsEnable)
                OnLog?.BeginInvoke(GetLogForNullReceived(), null, null);
        }

        /// <summary>
        /// Sends the packet to the network
        /// </summary>
        /// <param name="packet">The packet to send</param>
        protected abstract void Send(IrisPacket packet);

        /// <summary>
        /// Handler for an error received from the IrisListener
        /// </summary>
        /// <param name="meta">The IrisMeta received</param>
        protected abstract void OnMetaReceived(IrisMeta meta);
        #endregion
    }
}
