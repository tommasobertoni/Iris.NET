using Iris.NET.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Guid of this node.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// Triggers a log event when a message is received.
        /// </summary>
        public bool LogMessagesEnable { get; set; }

        /// <summary>
        /// Triggers a log event when an exception occurs.
        /// Default value is true.
        /// </summary>
        public bool LogExceptionsEnable { get; set; } = true;

        /// <summary>
        /// Triggers a log event when an error is received.
        /// Default value is true.
        /// </summary>
        public bool LogErrorsEnable { get; set; } = true;

        /// <summary>
        /// Triggers a log event when invalid data is received.
        /// Default value is true.
        /// </summary>
        public bool LogInvalidDataEnable { get; set; } = true;

        /// <summary>
        /// Triggers a log event when null is received.
        /// </summary>
        public bool LogNullsEnable { get; set; }
        #endregion

        #region Events
        /// <summary>
        /// Triggered when an exception occurs.
        /// </summary>
        public event ExceptionHandler OnException;
        public delegate void ExceptionHandler(Exception ex);

        /// <summary>
        /// Triggered when an operation generates a log.
        /// </summary>
        public event LogHandler OnLog;
        public delegate void LogHandler(string log);

        /// <summary>
        /// Triggered when the connection succeeded.
        /// </summary>
        public event VoidHandler OnConnected;

        /// <summary>
        /// Triggered when the client is disposed.
        /// </summary>
        public event VoidHandler OnDisposed;
        public delegate void VoidHandler();
        #endregion
        
        private AbstractIrisListener _subscriptionsListener;
        protected volatile IrisConcurrentHashSet<ContentHandler> _broadcastHandlers = new IrisConcurrentHashSet<ContentHandler>();
        protected volatile IChannelsSubscriptionsDictionary<ContentHandler> _channelsSubscriptions = new IrisChannelsSubscriptionsDictionary<ContentHandler>();
        protected bool _isDisposing;
        protected T _config;

        /// <summary>
        /// Used to store the last exception occurred, to avoid triggering multiple times
        /// the associate events if the exception occurs again right after
        /// </summary>
        protected Exception _lastException;

        #region Abstract
        /// <summary>
        /// Indicates if this node is connected.
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        /// Invoked when the node is connecting.
        /// </summary>
        /// <param name="config">The connection's configuration.</param>
        /// <returns>An AbstractIrisListener instance.</returns>
        protected abstract AbstractIrisListener OnConnect(T config);

        /// <summary>
        /// Invoked when the node is disposing.
        /// </summary>
        protected abstract void OnDispose();

        /// <summary>
        /// Sends the packet to the network.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        protected abstract void Send(IrisPacket packet);

        /// <summary>
        /// Handler for a meta packet received from the network.
        /// </summary>
        /// <param name="meta">The IrisMeta received.</param>
        protected abstract void OnMetaReceived(IrisMeta meta);
        #endregion

        #region Public
        /// <summary>
        /// Connects the node to it's network.
        /// </summary>
        /// <param name="config">The connection's configuration.</param>
        /// <returns>True if the operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Connect(T config)
        {
            if (IsConnected)
                return false;

            _config = config;
            _subscriptionsListener = OnConnect(_config);
            HookEventsToListener();
            _subscriptionsListener?.Start();

            OnConnected?.BeginInvoke(null, null);

            return true;
        }

        /// <summary>
        /// Closes the connection and disposes all the connection resources.
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

#if DEBUG && TEST
                // This is temporary...
                Console.WriteLine($"{this.GetType().Name}:{this.Id} STOPPED");
#endif
            }
        }

        #region PubSub
        /// <summary>
        /// Submits the content to the pubsub network.
        /// </summary>
        /// <param name="targetChannel">The channel targeted by the content.</param>
        /// <param name="content">The content to send.</param>
        /// <param name="propagateThroughHierarchy">Indicates if the content also targets all the clients who are subscribed to child channels compared to the target channel.</param>
        /// <returns>True if the operation succeeded.</returns>
        public virtual bool Send(string targetChannel, object content, bool propagateThroughHierarchy = false)
        {
            if (!IsConnected)
                return false;

            if (string.IsNullOrWhiteSpace(targetChannel))
                throw new ArgumentNullException(nameof(targetChannel));

            return SendUnsafe(targetChannel.ToLower(), content, propagateThroughHierarchy);
        }

        /// <summary>
        /// Submits the content to every node in the pubsub network.
        /// </summary>
        /// <param name="content">The content to send.</param>
        /// <returns>True if the operation succeeded.</returns>
        public bool SendToBroadcast(object content) => SendUnsafe(null, content, false);

        private bool SendUnsafe(string targetChannel, object content, bool propagateThroughHierarchy)
        {
            Send(new IrisMessage(Id, targetChannel, propagateThroughHierarchy)
            {
                PublicationDateTime = DateTime.Now,
                Content = content
            });

            return true;
        }

        /// <summary>
        /// Subscribes this node to a channel.
        /// </summary>
        /// <param name="channel">The channel to which subscribe.</param>
        /// <param name="contentHandler">A handler for the content received through this subscription.</param>
        /// <returns>An IDisposableSubscription which can be used to remove the content handler from the subscription, or null if the operation failed.</returns>
        public virtual IDisposableSubscription Subscribe(string channel, ContentHandler contentHandler)
        {
            if (!IsConnected)
                return null;

            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException(nameof(channel));

            if (contentHandler == null)
                throw new ArgumentNullException(nameof(contentHandler));

            channel = channel.ToLower();
            
            lock (_channelsSubscriptions)
            {
                if (_channelsSubscriptions.Add(contentHandler, channel))
                {
                    Send(new IrisSubscribe(Id, channel));
                    return new IrisDisposableSubscription(this, channel, contentHandler);
                }
            }

            return null; // #Note Return already disposed IrisDisposableSubscription?
        }

        /// <summary>
        /// Subscribes the conten handler to the broadcast communication.
        /// </summary>
        /// <param name="contentHandler">A handler for the content received in broadcast.</param>
        /// <returns>An IDisposableSubscription which can be used to remove the content handler from the broadcast, or null if the operation failed.</returns>
        public IDisposableSubscription SubscribeToBroadcast(ContentHandler contentHandler)
        {
            if (!IsConnected)
                return null;

            if (contentHandler == null)
                throw new ArgumentNullException(nameof(contentHandler));
            
            lock (_broadcastHandlers)
            {
                if (_broadcastHandlers.Add(contentHandler))
                    return new IrisDisposableSubscription(this, null, contentHandler);
            }

            return null; // #Note Return already disposed IrisDisposableSubscription?
        }

        /// <summary>
        /// Removes the content handler from the subscription.
        /// </summary>
        /// <param name="channel">The channel from which unsubscribe.</param>
        /// <param name="contentHandler">The content handler to be removed from this subscription.</param>
        /// <param name="keepUnderlyingSubscription">Indicates if the node should keep the underlying subscription to the channel in order to improve efficiency in future subscriptions to it.</param>
        /// <returns>True if the operation succeeded.</returns>
        public virtual bool Unsubscribe(string channel, ContentHandler contentHandler, bool keepUnderlyingSubscription = false)
        {
            if (!IsConnected)
                return false;

            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException(nameof(channel));

            if (contentHandler == null)
                throw new ArgumentNullException(nameof(contentHandler));

            channel = channel.ToLower();

            bool result = false;
            lock (_channelsSubscriptions)
            {
                if (result = _channelsSubscriptions.Remove(contentHandler, channel))
                {
                    contentHandler.BeginInvoke(null, new IrisContextHook { Unsubscribing = true }, null, null);
                    
                    // If there aren't any subscriptions and there's no request of keeping the underlying subscription
                    if (!_channelsSubscriptions.GetSubscriptions(channel, true).Any() && !keepUnderlyingSubscription)
                    {
                        Send(new IrisUnsubscribe(Id, channel));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Removes the content handler from the broadcast communication.
        /// </summary>
        /// <param name="contentHandler">The content handler to be removed from the broadcast.</param>
        /// <returns>True if the operation succeeded.</returns>
        public bool UnsubscribeFromBroadcast(ContentHandler contentHandler)
        {
            if (!IsConnected)
                return false;

            if (contentHandler == null)
                throw new ArgumentNullException(nameof(contentHandler));

            bool result = false;
            lock (_broadcastHandlers)
            {
                if (result = _broadcastHandlers.Remove(contentHandler))
                    contentHandler.BeginInvoke(null, new IrisContextHook { Unsubscribing = true }, null, null);
            }

            return result;
        }

        /// <summary>
        /// Unsubscribes this node from a channel.
        /// All the content handlers subscribed to this channel will be lost.
        /// </summary>
        /// <param name="channel">The channel from which unsubscribe.</param>
        /// <returns>True if the operation succeeded.</returns>
        public virtual bool Unsubscribe(string channel)
        {
            if (!IsConnected)
                return false;

            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException(nameof(channel));

            channel = channel.ToLower();

            lock (_channelsSubscriptions)
            {
                List<ContentHandler> subs = _channelsSubscriptions.GetSubscriptions(channel);
                if (subs != null && _channelsSubscriptions.RemoveChannel(channel))
                {
                    Send(new IrisUnsubscribe(Id, channel));
                    return true;
                }
            }

            return false;
        }
        #endregion
        #endregion

        /// <summary>
        /// Attaches handlers to the IrisListener events.
        /// </summary>
        /// <param name="subscriptionsListener">The target IrisListener.</param>
        protected void HookEventsToListener(AbstractIrisListener subscriptionsListener = null)
        {
            if (subscriptionsListener == null)
                subscriptionsListener = _subscriptionsListener;
            
            if (subscriptionsListener != null)
            {
                subscriptionsListener.OnClientSubmittedPacketReceived += OnClientSubmittedPacketReceived;
                subscriptionsListener.OnMetaReceived += OnMetaReceived;
                subscriptionsListener.OnErrorReceived += OnError;
                subscriptionsListener.OnInvalidDataReceived += OnInvalidDataReceived;
                subscriptionsListener.OnException += HandleListenerException;
                subscriptionsListener.OnNullReceived += OnNullReceived;
            }
        }

        /// <summary>
        /// Detaches the handlers from the IrisListener events.
        /// </summary>
        /// <param name="subscriptionsListener">The target IrisListener.</param>
        protected void UnhookEventsFromListener(AbstractIrisListener subscriptionsListener = null)
        {
            if (subscriptionsListener == null)
                subscriptionsListener = _subscriptionsListener;

            if (subscriptionsListener != null)
            {
                subscriptionsListener.OnClientSubmittedPacketReceived -= OnClientSubmittedPacketReceived;
                subscriptionsListener.OnMetaReceived -= OnMetaReceived;
                subscriptionsListener.OnErrorReceived -= OnError;
                subscriptionsListener.OnInvalidDataReceived -= OnInvalidDataReceived;
                subscriptionsListener.OnException -= HandleListenerException;
                subscriptionsListener.OnNullReceived -= OnNullReceived;
            }
        }

        /// <summary>
        /// Checks if the connection is working properly by sending a meta packet.
        /// </summary>
        /// <returns></returns>
        protected bool IsPeerAlive()
        {
            try
            {
                Send(new IrisMeta(Id)
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

        #region Listener events
        /// <summary>
        /// Builds a string for logging the invalid data received.
        /// </summary>
        /// <param name="data">The invalid data received.</param>
        /// <returns>A log string.</returns>
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
        /// Handler for invalid data received from the IrisListener.
        /// Fires a log event if LogInvalidDataEnable is true.
        /// </summary>
        /// <param name="data">The invalid data received.</param>
        protected virtual void OnInvalidDataReceived(object data)
        {
            if (LogInvalidDataEnable)
                OnLog?.BeginInvoke(GetLogForInvalidDataReceived(data), null, null);
        }

        /// <summary>
        /// Builds a string for logging the exception that occurred.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        /// <returns>A log string.</returns>
        protected string GetLogForException(Exception ex) => $"[Exception];{ex.GetFullExceptionMessage()}";

        /// <summary>
        /// Handler for exceptions coming from the IrisListener.
        /// Checks wether this exception was already caught right before and invokes the method OnListenerException.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        private void HandleListenerException(Exception ex)
        {
            if (_lastException == null || ex.Message != _lastException.Message)
            {
                _lastException = ex;
                OnListenerException(ex);
            }
        }

        /// <summary>
        /// Handler for exceptions coming from the IrisListener.
        /// Fires a OnException event.
        /// Fires a log event if LogExceptionsEnable is true.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        protected virtual void OnListenerException(Exception ex)
        {
            OnException?.BeginInvoke(ex, null, null);
            if (LogExceptionsEnable)
                OnLog?.BeginInvoke(GetLogForException(ex), null, null);
        }

        /// <summary>
        /// Builds a string for logging the error received.
        /// </summary>
        /// <param name="error">The IrisError received.</param>
        /// <returns>A log string.</returns>
        protected string GetLogForError(IrisError error) => $"[Error];{nameof(error.PublisherId)}: {error.PublisherId};{nameof(error.Exception)}: {error.Exception?.GetFullExceptionMessage()}";

        /// <summary>
        /// Handler for an IrisError.
        /// Fires a log event if LogErrorsEnable is true.
        /// </summary>
        /// <param name="error">The IrisError received.</param>
        protected virtual void OnError(IrisError error)
        {
            if (LogErrorsEnable)
                OnLog?.BeginInvoke(GetLogForError(error), null, null);
        }

        /// <summary>
        /// Builds a string for logging the message received.
        /// </summary>
        /// <param name="message">The IrisMessage received.</param>
        /// <returns>A log string.</returns>
        protected virtual string GetLogForMessageReceived(IrisMessage message) => $"[Message];{nameof(message.TargetChannel)}: {message.TargetChannel};{nameof(message.PublicationDateTime)}: {message.PublicationDateTime};{nameof(message.PropagateThroughHierarchy)}: {message.PropagateThroughHierarchy}";

        /// <summary>
        /// Handler for a packet received from the IrisListener.
        /// If the packet is of IrisMessage type, invokes the ContentHandlers subscribed to the reception of the message.
        /// Fires a log event if LogMessagesEnable is true and the packet is of IrisMessage type.
        /// </summary>
        /// <param name="packet">The packet received.</param>
        protected virtual void OnClientSubmittedPacketReceived(IrisPacket packet)
        {
            if (packet is IrisMessage)
            {
                var message = packet as IrisMessage;
                IEnumerable<ContentHandler> handlers = null;

                if (message.TargetChannel != null)
                    handlers = _channelsSubscriptions.GetSubscriptions(message.TargetChannel, message.PropagateThroughHierarchy);
                else
                {
                    lock (_broadcastHandlers)
                    {
                        var broadcastHandlers = new List<ContentHandler>();
                        broadcastHandlers.AddRange(_broadcastHandlers);
                        handlers = broadcastHandlers;
                    }
                }

                if (handlers != null)
                {
                    var ctxHook = new IrisContextHook()
                    {
                        TargetChannel = message.TargetChannel,
                        PublicationDateTime = message.PublicationDateTime
                    };

                    foreach (var hander in handlers)
                        hander.BeginInvoke(message.Content, ctxHook, null, null);
                }

                if (LogMessagesEnable)
                    OnLog?.BeginInvoke(GetLogForMessageReceived(message), null, null);

                // No subscription found for this message
                if (handlers == null)
                    OnLog?.BeginInvoke(GetLogForError(new IrisError(Id)
                    {
                        Log = $"Unable to find subscriptions for the channel {message.TargetChannel}. [Message]{GetLogForMessageReceived(message)}."
                    }), null, null);
            }
        }

        /// <summary>
        /// Builds a string for logging null data received.
        /// </summary>
        /// <returns>A log string.</returns>
        protected virtual string GetLogForNullReceived() => $"[NullReceived] Node: {Id}";

        /// <summary>
        /// Handler for null data received from the IrisListener.
        /// Fires a log event if LogNullsEnable is true.
        /// </summary>
        protected virtual void OnNullReceived()
        {
            if (LogNullsEnable)
                OnLog?.BeginInvoke(GetLogForNullReceived(), null, null);
        }
        #endregion
    }
}
