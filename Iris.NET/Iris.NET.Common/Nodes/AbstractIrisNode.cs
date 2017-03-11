using Iris.NET.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Iris.NET
{
    /// <summary>
    /// Abstract implementation of IIrisNode, with the addition of events and connection methods.
    /// It also provides a method for testing if the connection is working properly: IsPeerAlive().
    /// </summary>
    public abstract class AbstractIrisNode<T> : IIrisNode
           where T : IrisBaseConfig
    {
        #region Static
        static readonly IrisDisposableSubscription _disposedSubscription = new IrisDisposableSubscription();

        static readonly Task<bool> _completedSucceededTask = TaskEx.FromResult(true);

        static readonly Task<bool> _completedFailedTask = TaskEx.FromResult(false);
        #endregion

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

        /// <summary>
        /// Delegate for the OnException event.
        /// </summary>
        /// <param name="ex">The exception to be handled.</param>
        public delegate void ExceptionHandler(Exception ex);

        /// <summary>
        /// Triggered when an operation generates a log.
        /// </summary>
        public event LogHandler OnLog;

        /// <summary>
        /// Delegate for the OnLog event.
        /// </summary>
        /// <param name="log">The log to be handled.</param>
        public delegate void LogHandler(string log);

        /// <summary>
        /// Triggered when the connection succeeded.
        /// </summary>
        public event VoidHandler OnConnected;

        /// <summary>
        /// Triggered when the client is disposed.
        /// </summary>
        public event VoidHandler OnDisposed;

        /// <summary>
        /// Delegate for the OnConnected and OnDisposed events.
        /// </summary>
        public delegate void VoidHandler();
        #endregion

        /// <summary>
        /// Set of content handlers for the broadcast communication.
        /// </summary>
        protected volatile IrisConcurrentHashSet<ContentHandler> _broadcastHandlers = new IrisConcurrentHashSet<ContentHandler>();

        /// <summary>
        /// Instance of IChannelsSubscriptionsDictionary used to store content handlers subscriptions.
        /// </summary>
        protected volatile IChannelsSubscriptionsDictionary<ContentHandler> _channelsSubscriptions = new IrisChannelsSubscriptionsDictionary<ContentHandler>();

        /// <summary>
        /// Indicates if this node is disposed.
        /// </summary>
        protected bool _isDisposed;

        /// <summary>
        /// The Iris configuration for this node.
        /// </summary>
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
        protected abstract void OnConnect(T config);

        /// <summary>
        /// Invoked when the node is disposing.
        /// </summary>
        protected abstract void OnDispose();

        /// <summary>
        /// Publishes the packet to the network.
        /// </summary>
        /// <param name="packet">The packet to publish.</param>
        protected abstract Task<bool> Publish(IrisPacket packet);
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
            OnConnect(_config);
            OnConnected?.BeginInvoke(null, null);

            return true;
        }

        /// <summary>
        /// Closes the connection and disposes all the connection resources.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                OnDispose();
                OnDisposed?.BeginInvoke(null, null);
            }
        }

        #region PubSub
        /// <summary>
        /// Publish the content to the pubsub network asynchronously.
        /// </summary>
        /// <param name="targetChannel">The channel targeted by the content. If it is "null" the content targets every client (broadcast).</param>
        /// <param name="content">The content to publish.</param>
        /// <param name="propagateThroughHierarchy">Indicates if the content also targets all the clients who are subscribed to child channels compared to the target channel.</param>
        /// <returns>A task which value is true if the operation succeeded.</returns>
        public virtual Task<bool> Publish(string targetChannel, object content, bool propagateThroughHierarchy = false)
        {
            if (!IsConnected)
                throw new NodeDisconnectedException();

            if (string.IsNullOrWhiteSpace(targetChannel))
                throw new ArgumentNullException(nameof(targetChannel));

            return PublishUnsafe(targetChannel.ToLower(), content, propagateThroughHierarchy);
        }

        /// <summary>
        /// Submits the content to every node in the pubsub network asynchronously.
        /// </summary>
        /// <param name="content">The content to publish.</param>
        /// <returns>A task which value is true if the operation succeeded.</returns>
        public Task<bool> PublishToBroadcast(object content) => PublishUnsafe(null, content, false);

        private Task<bool> PublishUnsafe(string targetChannel, object content, bool propagateThroughHierarchy)
        {
            return Publish(new IrisMessage(Id, targetChannel, propagateThroughHierarchy)
            {
                PublicationDateTime = DateTime.Now,
                Content = content
            });
        }

        /// <summary>
        /// Subscribes this node to a channel asynchronously.
        /// </summary>
        /// <param name="channel">The channel to which subscribe.</param>
        /// <param name="contentHandler">A handler for the content received through this subscription.</param>
        /// <returns>A task which value is an IDisposableSubscription which can be used to remove the content handler from the subscription, or null if the operation failed.</returns>
        public virtual async Task<IDisposableSubscription> Subscribe(string channel, ContentHandler contentHandler)
        {
            if (!IsConnected)
                throw new NodeDisconnectedException();

            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException(nameof(channel));

            if (contentHandler == null)
                throw new ArgumentNullException(nameof(contentHandler));

            channel = channel.ToLower();
            
            if (_channelsSubscriptions.Add(contentHandler, channel))
            {
                if (await Publish(new IrisSubscribe(Id, channel)))
                    return new IrisDisposableSubscription(this, channel, contentHandler);
            }

            return _disposedSubscription;
        }

        /// <summary>
        /// Subscribes the conten handler to the broadcast communication asynchronously.
        /// </summary>
        /// <param name="contentHandler">A handler for the content received in broadcast.</param>
        /// <returns>A task which value is an IDisposableSubscription which can be used to remove the content handler from the broadcast, or null if the operation failed.</returns>
        public Task<IDisposableSubscription> SubscribeToBroadcast(ContentHandler contentHandler)
        {
            if (!IsConnected)
                throw new NodeDisconnectedException();

            if (contentHandler == null)
                throw new ArgumentNullException(nameof(contentHandler));
            
            if (_broadcastHandlers.Add(contentHandler))
            {
                return TaskEx.FromResult<IDisposableSubscription>(new IrisDisposableSubscription(this, null, contentHandler));
            }

            return TaskEx.FromResult<IDisposableSubscription>(_disposedSubscription);
        }

        /// <summary>
        /// Removes the content handler from the subscription asynchronously.
        /// </summary>
        /// <param name="channel">The channel from which unsubscribe.</param>
        /// <param name="contentHandler">The content handler to be removed from this subscription.</param>
        /// <param name="keepUnderlyingSubscription">Indicates if the node should keep the underlying subscription to the channel in order to improve efficiency in future subscriptions to it.</param>
        /// <returns>A task which value is true if the operation succeeded.</returns>
        public virtual Task<bool> Unsubscribe(string channel, ContentHandler contentHandler, bool keepUnderlyingSubscription = false)
        {
            if (!IsConnected)
                throw new NodeDisconnectedException();

            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException(nameof(channel));

            if (contentHandler == null)
                throw new ArgumentNullException(nameof(contentHandler));

            channel = channel.ToLower();
            
            if (_channelsSubscriptions.Remove(contentHandler, channel))
            {
                contentHandler.BeginInvoke(null, new IrisContextHook { Unsubscribing = true }, null, null);
                    
                // If there aren't any subscriptions and there's no request of keeping the underlying subscription
                if (!_channelsSubscriptions.GetSubscriptions(channel, true).Any() && !keepUnderlyingSubscription)
                {
                    return Publish(new IrisUnsubscribe(Id, channel));
                }
            }

            return _completedFailedTask;
        }

        /// <summary>
        /// Removes the content handler from the broadcast communication asynchronously.
        /// </summary>
        /// <param name="contentHandler">The content handler to be removed from the broadcast.</param>
        /// <returns>A task which value is true if the operation succeeded.</returns>
        public Task<bool> UnsubscribeFromBroadcast(ContentHandler contentHandler)
        {
            if (!IsConnected)
                throw new NodeDisconnectedException();

            if (contentHandler == null)
                throw new ArgumentNullException(nameof(contentHandler));
            
            if (_broadcastHandlers.Remove(contentHandler))
            {
                contentHandler.BeginInvoke(null, new IrisContextHook { Unsubscribing = true }, null, null);
                return _completedSucceededTask;
            }

            return _completedFailedTask;
        }

        /// <summary>
        /// Unsubscribes this node from a channel asynchronously.
        /// All the content handlers subscribed to this channel will be lost.
        /// </summary>
        /// <param name="channel">The channel from which unsubscribe.</param>
        /// <returns>A task which value is true if the operation succeeded.</returns>
        public virtual Task<bool> Unsubscribe(string channel)
        {
            if (!IsConnected)
                throw new NodeDisconnectedException();

            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentNullException(nameof(channel));

            channel = channel.ToLower();

            List<ContentHandler> subs = _channelsSubscriptions.GetSubscriptions(channel);
            if (subs != null && _channelsSubscriptions.RemoveChannel(channel))
            {
                return Publish(new IrisUnsubscribe(Id, channel));
            }

            return _completedFailedTask;
        }
        #endregion
        #endregion

        /// <summary>
        /// Checks if the connection is working properly by publishing a meta packet.
        /// </summary>
        /// <returns></returns>
        protected Task<bool> IsPeerAlive()
        {
            try
            {
                return Publish(new IrisMeta(Id)
                {
                    ACK = null,
                    Request = Request.AreYouAlive
                });
            }
            catch
            {
                return _completedFailedTask;
            }
        }

        #region Network communication utils
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
        /// Handler for invalid data received.
        /// Fires a log event if LogInvalidDataEnable is true.
        /// </summary>
        /// <param name="data">The invalid data received.</param>
        protected virtual void OnInvalidDataReceived(object data)
        {
            if (LogInvalidDataEnable)
                OnLog?.BeginInvoke(GetLogForInvalidDataReceived(data), null, null);
        }

        /// <summary>
        /// Handler for a meta packet received from the network.
        /// </summary>
        /// <param name="meta">The IrisMeta received.</param>
        protected virtual void OnMetaReceived(IrisMeta meta)
        {
        }

        /// <summary>
        /// Builds a string for logging the exception that occurred.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        /// <returns>A log string.</returns>
        protected string GetLogForException(Exception ex) => $"[Exception];{ex.GetFullExceptionMessage()}";

        /// <summary>
        /// Handler for exceptions coming from the network.
        /// Checks wether this exception was already caught right before and invokes the method OnNetworkException.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        protected void HandleException(Exception ex)
        {
            if (_lastException == null || ex.Message != _lastException.Message)
            {
                _lastException = ex;
                OnNetworkException(ex);
            }
        }

        /// <summary>
        /// Handler for exceptions coming from the network.
        /// Fires a OnException event.
        /// Fires a log event if LogExceptionsEnable is true.
        /// </summary>
        /// <param name="ex">The exception that occurred.</param>
        protected virtual void OnNetworkException(Exception ex)
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
        /// Handler for a packet received from the network.
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
                    var broadcastHandlers = new List<ContentHandler>();
                    broadcastHandlers.AddRange(_broadcastHandlers);
                    handlers = broadcastHandlers;
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
        /// Handler for null data received from the network.
        /// Fires a log event if LogNullsEnable is true.
        /// </summary>
        protected virtual void OnNullReceived()
        {
            if (LogNullsEnable)
                OnLog?.BeginInvoke(GetLogForNullReceived(), null, null);
        }
        #endregion
    }

    /// <summary>
    /// Exception used to indicate that the operation cannot be performed since the node is currently disconnected from the pubsub network.
    /// </summary>
    public class NodeDisconnectedException : Exception
    {
    }
}
