using System;
using System.Collections.Generic;
using System.Text;

namespace Iris.NET
{
    public abstract class AbstractIrisNode<T> : IrisNode
           where T : IrisConfig
    {
        internal AbstractIrisListener _subscriptionsListener;
        protected volatile Dictionary<string, LinkedList<ContentHandler>> _channelsSubscriptions = new Dictionary<string, LinkedList<ContentHandler>>();

        public abstract bool IsConnected { get; }

        public Guid ClientId { get; } = Guid.NewGuid();

        public abstract bool Connect(T config);

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
        public virtual bool SendAsync(string channel, object content, bool propagateThroughHierarchy = false)
        {
            if (!IsConnected)
                return false;

            var message = new IrisMessage(ClientId, channel, propagateThroughHierarchy);
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

            var sub = new IrisSubscribe(ClientId, channel);
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
                        var unsub = new IrisUnsubscribe(ClientId, channel);
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
        protected virtual void OnInvalidDataReceived(object data)
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
            var message = $"[InvalidDataReceived];Runtime Type:{runtimeType}";
            OnLog?.BeginInvoke(message, null, null);
        }

        protected virtual void OnListenerException(Exception ex)
        {
            OnException?.BeginInvoke(ex, null, null);
            var message = $"[Exception];{ex.GetFullException()}";
            OnLog?.BeginInvoke(message, null, null);
        }

        protected virtual void OnErrorReceived(IrisError error)
        {
            var message = $"[Error];{nameof(error.PublisherId)}: {error.PublisherId};{nameof(error.ByException)}: {error.ByException};{nameof(error.Exception)}: {error.Exception?.GetFullException()}";
            OnLog?.BeginInvoke(message, null, null);
        }

        protected void OnMessageReceived(IrisMessage message)
        {
            LinkedList<ContentHandler> subscriptions;
            if (_channelsSubscriptions.TryGetValue(message.TargetChannel, out subscriptions))
            {
                foreach (var subscription in subscriptions)
                    subscription.BeginInvoke(message.Content, null, null);
            }

            OnLog?.BeginInvoke($"[Message];{nameof(message.TargetChannel)}: {message.TargetChannel};{nameof(message.PublicationDateTime)}: {message.PublicationDateTime};{nameof(message.PropagateThroughHierarchy)}: {message.PropagateThroughHierarchy}", null, null);
        }

        protected abstract void Send(IrisPacket packet);
        #endregion

        public virtual void Dispose() => UnhookEventsFromListener();
    }
}
