using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Iris.NET
{
    /// <summary>
    /// Abstract class that listens to incoming data and deserializes it into packets and sends them
    /// through events. Handles errors and exceptions.
    /// The listening cycle is on a different thread.
    /// </summary>
    public abstract class AbstractIrisListener
    {
        #region Properties
        /// <summary>
        /// Indicates if it's listening to incoming data.
        /// </summary>
        public bool IsListening => _thread != null && _keepListening;
        #endregion

        protected Thread _thread;
        private volatile bool _keepListening;

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
        internal delegate void VoidHandler();
        #endregion

        #region Abstract
        /// <summary>
        /// Initialize the listening cycle.
        /// </summary>
        protected abstract void InitListenCycle();

        /// <summary>
        /// Reads the incoming data and return it as object.
        /// </summary>
        /// <returns>The data as object</returns>
        protected abstract object ReadObject();

        /// <summary>
        /// Invoked when this listener is stopping.
        /// </summary>
        protected abstract void OnStop();
        #endregion

        #region Public
        /// <summary>
        /// Starts the listening cycle.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual void Start()
        {
            if (!IsListening)
            {
                _keepListening = true;
                _thread = new Thread(Listen);
                _thread.Start();
                // Loop until worker thread activates.
                while (!_thread.IsAlive) ;
            }
        }

        /// <summary>
        /// Stops the listening cycle.
        /// </summary>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual void Stop()
        {
            if (IsListening)
            {
                _keepListening = false;
                OnStop();
                _thread.Join();
                _thread = null;
            }
        }
        #endregion

        /// <summary>
        /// Executes the listening cycle. When some data is received, the appropriate event is fired
        /// in order to notify whoever is listening for the data using this instance.
        /// </summary>
        protected virtual void Listen()
        {
            while (_keepListening)
            {
                object data = null;
                InitListenCycle();

                try
                {
                    data = ReadObject();

                    if (_keepListening)
                    {
                        if (data == null)
                            OnNullReceived?.BeginInvoke(null, null);
                        else if (data is IrisError)
                            OnErrorReceived?.BeginInvoke((IrisError)data, null, null);
                        else if (data is IrisMeta)
                            OnMetaReceived?.BeginInvoke((IrisMeta)data, null, null);
                        else
                        {
                            var packet = (IrisPacket)data;
                            if (packet.IsClientSubmitted)
                                OnClientSubmittedPacketReceived?.BeginInvoke(packet, null, null);
                            else
                                OnInvalidDataReceived?.BeginInvoke(data, null, null);
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    OnInvalidDataReceived?.BeginInvoke(data, null, null);
                }
                catch (Exception ex)
                {
                    OnException?.BeginInvoke(ex, null, null);
                }
            }
        }
    }
}
