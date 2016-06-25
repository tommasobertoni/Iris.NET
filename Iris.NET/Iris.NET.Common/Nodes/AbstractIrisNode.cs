using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    public abstract class AbstractIrisNode<T> : IrisNode
           where T : IrisBaseConfig
    {
        public bool LogMessagesEnable { get; set; }

        public bool LogExceptionsEnable { get; set; } = true;

        public bool LogErrorsEnable { get; set; } = true;

        public bool LogInvalidDataEnable { get; set; } = true;

        private AbstractIrisListener _subscriptionsListener;
        protected volatile Dictionary<string, LinkedList<ContentHandler>> _channelsSubscriptions = new Dictionary<string, LinkedList<ContentHandler>>();

        protected T _config;

        public Guid NodeId { get; } = Guid.NewGuid();

        public abstract bool IsConnected { get; }
        
        protected abstract AbstractIrisListener OnConnect(T config);

        public bool Connect(T config)
        {
            _config = config;
            _subscriptionsListener = OnConnect(_config);
            HookEventsToListener();
            _subscriptionsListener?.Start();
            return _subscriptionsListener != null;
        }

        protected void HookEventsToListener(AbstractIrisListener subscriptionsListener = null)
        {
            if (subscriptionsListener == null)
                subscriptionsListener = _subscriptionsListener;
            
            if (subscriptionsListener != null)
            {
                subscriptionsListener.OnMessageReceived += OnMessageReceived;
                subscriptionsListener.OnErrorReceived += OnErrorReceived;
                subscriptionsListener.OnInvalidDataReceived += OnInvalidDataReceived;
                subscriptionsListener.OnException += OnListenerException;
            }
        }

        protected void UnhookEventsFromListener(AbstractIrisListener subscriptionsListener = null)
        {
            if (subscriptionsListener == null)
                subscriptionsListener = _subscriptionsListener;

            if (subscriptionsListener != null)
            {
                subscriptionsListener.OnMessageReceived -= OnMessageReceived;
                subscriptionsListener.OnErrorReceived -= OnErrorReceived;
                subscriptionsListener.OnInvalidDataReceived -= OnInvalidDataReceived;
                subscriptionsListener.OnException -= OnListenerException;
            }
        }

        #region PubSub
        public virtual bool Send(string channel, object content, bool propagateThroughHierarchy = false)
        {
            if (!IsConnected)
                return false;

            var message = new IrisMessage(NodeId, channel, propagateThroughHierarchy);
            message.PublicationDateTime = DateTime.Now;
            message.Content = content;
            Send(message);

            return true;
        }

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

        public virtual bool Unsubscribe(string channel, ContentHandler messageHandler)
        {
            if (!IsConnected)
                return false;

            lock (_channelsSubscriptions)
            {
                LinkedList<ContentHandler> subs = null;
                if (_channelsSubscriptions.TryGetValue(channel, out subs))
                {
                    if (subs.Remove(messageHandler))
                    {
                        var unsub = new IrisUnsubscribe(NodeId, channel);
                        Send(unsub);
                        return true;
                    }
                }
            }

            return false;
        }
        #endregion

        #region Events
        public delegate void ExceptionHandler(Exception ex);
        public event ExceptionHandler OnException;

        public delegate void LogHandler(string log);
        public event LogHandler OnLog;
        #endregion

        #region Messages handling
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

        protected virtual void OnInvalidDataReceived(object data)
        {
            if (LogInvalidDataEnable)
                OnLog?.BeginInvoke(GetLogForInvalidDataReceived(data), null, null);
        }

        protected string GetLogForException(Exception ex) => $"[Exception];{ex.GetFullException()}";

        protected virtual void OnListenerException(Exception ex)
        {
            if (LogExceptionsEnable)
            {
                OnException?.BeginInvoke(ex, null, null);
                OnLog?.BeginInvoke(GetLogForException(ex), null, null);
            }
        }

        protected string GetLogForErrorReceived(IrisError error) => $"[Error];{nameof(error.PublisherId)}: {error.PublisherId};{nameof(error.ByException)}: {error.ByException};{nameof(error.Exception)}: {error.Exception?.GetFullException()}";

        protected virtual void OnErrorReceived(IrisError error)
        {
            if (LogErrorsEnable)
                OnLog?.BeginInvoke(GetLogForErrorReceived(error), null, null);
        }

        protected virtual string GetLogForMessageReceived(IrisMessage message) => $"[Message];{nameof(message.TargetChannel)}: {message.TargetChannel};{nameof(message.PublicationDateTime)}: {message.PublicationDateTime};{nameof(message.PropagateThroughHierarchy)}: {message.PropagateThroughHierarchy}";

        protected virtual void OnMessageReceived(IUserSubmittedPacket packet)
        {
            if (packet is IrisMessage)
            {
                var message = packet as IrisMessage;
                LinkedList<ContentHandler> subscriptions;
                if (_channelsSubscriptions.TryGetValue(message.TargetChannel, out subscriptions))
                {
                    foreach (var subscription in subscriptions)
                        subscription.BeginInvoke(message.Content, null, null);
                }

                if (LogMessagesEnable)
                    OnLog?.BeginInvoke(GetLogForMessageReceived(message), null, null);
            }
        }

        protected abstract void Send(IrisPacket packet);
        #endregion

        public virtual void Dispose()
        {
            UnhookEventsFromListener();
            _subscriptionsListener?.Stop();
        }
    }
}
