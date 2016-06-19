using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Iris.NET
{
    public abstract class AbstractIrisListener
    {
        public bool IsListening => _thread != null && _keepListening;

        protected Thread _thread;
        private volatile bool _keepListening;
        protected int _failureAttempts;

        public AbstractIrisListener(int failureAttempts = 1)
        {
            _failureAttempts = failureAttempts;
        }

        #region Events
        internal delegate void InvalidDataHandler(object data);
        internal event InvalidDataHandler OnInvalidDataReceived;

        internal delegate void ExceptionHandler(Exception ex);
        internal event ExceptionHandler OnException;

        internal delegate void ErrorHandler(IrisError error);
        internal event ErrorHandler OnErrorReceived;

        internal delegate void MessageHandler(IrisMessage message);
        internal event MessageHandler OnMessageReceived;
        #endregion

        public virtual void Start()
        {
            _thread = new Thread(Listen);
            _thread.Start();
            // Loop until worker thread activates.
            while (!_thread.IsAlive) ;
        }

        protected abstract void InitListenCycle();

        protected abstract object ReadObject();

        protected virtual void Listen()
        {
            while (_keepListening)
            {
                object data = null;
                InitListenCycle();

                try
                {
                    data = ReadObject();

                    if (data is IrisError)
                        OnErrorReceived.BeginInvoke(data as IrisError, null, null);
                    else
                        OnMessageReceived.BeginInvoke(data as IrisMessage, null, null);
                }
                catch (InvalidCastException)
                {
                    OnInvalidDataReceived.BeginInvoke(data, null, null);
                }
                catch (Exception ex)
                {
                    OnException.BeginInvoke(ex, null, null);
                }
            }
        }

        public virtual void Stop()
        {
            _keepListening = false;
            _thread.Join();
            _thread = null;
        }
    }
}
