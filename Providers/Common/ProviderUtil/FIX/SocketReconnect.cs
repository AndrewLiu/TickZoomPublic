using System;
using System.Diagnostics;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public class SocketReconnect : IDisposable, LogAware
    {
        private readonly Log log;
        private volatile bool trace;
        private volatile bool debug;
        public virtual void RefreshLogLevel()
        {
            if (log != null)
            {
                debug = log.IsDebugEnabled;
                trace = log.IsTraceEnabled;
            }
        }
        private Socket socket;
        private long retryDelay = 30; // seconds
        private long retryStart = 30; // seconds
        private Task task;
        private string address;
        private int port;
        private TrueTimer retryTimer;
        private MessageFactory messageFactory;
        private long retryIncrease = 5;
        private long retryMaximum = 30;
        protected bool fastRetry = false;
        private Action onDisconnect;
        private Action onConnect;

        public SocketReconnect(string providerName, string config, Task task, string address, int port, MessageFactory messageFactory, Action onConnect, Action onDisconnect)
        {
            log = Factory.SysLog.GetLogger(typeof(SocketReconnect) + "." + providerName + "." + config);
            log.Register(this);
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;

            this.task = task;
            this.address = address;
            this.port = port;
            this.messageFactory = messageFactory;
            this.retryTimer = Factory.Parallel.CreateTimer("Retry", task, RetryTimerEvent);
            this.onDisconnect = onDisconnect;
            this.onConnect = onConnect;
        }

        public void Regenerate()
        {
            if (debug) log.DebugFormat("Regenerate.");
            Socket old = socket;
            if (socket != null)
            {
                socket.Dispose();
                switch (socket.State)
                {
                    case SocketState.Connected:
                    case SocketState.Bound:
                    case SocketState.Listening:
                    case SocketState.ShuttingDown:
                    case SocketState.Disconnected:
                    case SocketState.New:
                    case SocketState.PendingConnect:
                        if (debug) log.DebugFormat("Wait for graceful socket shutdown because socket state: " + socket.State);
                        return;
                    case SocketState.Closing:
                    case SocketState.Closed:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("Unexpected socket state: " + socket.State);
                }
            }
            socket = Factory.Provider.Socket(this.GetType().Name + "Socket", address, port);
            socket.ReceiveQueue.ConnectInbound(task);
            socket.SendQueue.ConnectOutbound(task);
            socket.OnConnect = OnConnect;
            socket.MessageFactory = messageFactory;
            if (debug) log.DebugFormat("Created new " + socket);
            if (trace)
            {
                string message = "Generated socket: " + socket;
                if (old != null)
                {
                    message += " to replace: " + old;
                }
                log.Trace(message);
            }
            if (SyncTicks.Enabled)
            {
                RetryDelay = 1;
                RetryStart = 1;
            }
            else
            {
                RetryDelay = 30;
            }
            // Initiate socket connection.
            while (true)
            {
                try
                {
                    socket.Connect();
                    if (debug) log.DebugFormat("Requested Connect for " + socket);
                    SetupRetry();
                    return;
                }
                catch (SocketErrorException ex)
                {
                    log.Error("Non fatal error while trying to connect: " + ex.Message);
                }
            }
        }

        public void IncreaseRetry()
        {
            RetryDelay += retryIncrease;
            RetryDelay = Math.Min(RetryDelay, retryMaximum);
            SetupRetry();
        }

        public void FastRetry()
        {
            RetryStart = 1;
            fastRetry = true;
            SetupRetry();
        }

        private void SetupRetry()
        {
            var startTime = Factory.Parallel.UtcNow;
            var fastRetryDelay = 1;
            var retryDelay = fastRetry ? fastRetryDelay : RetryDelay;
            if (SyncTicks.Enabled)
            {
                startTime.AddMilliseconds(retryDelay * 100);
            }
            else
            {
                startTime.AddSeconds(retryDelay);
            }
            retryTimer.Start(startTime);
            if (fastRetry) log.InfoFormat("Quick retry requested.  Connection will retry in {0} seconds", fastRetryDelay);
            else
                log.Info("Connection will timeout and retry in " + RetryDelay + " seconds.");
            fastRetry = false;
        }

        public Message CreateMessage()
        {
            return socket.MessageFactory.Create();
        }

        public bool TryGetMessage( out Message message)
        {
            return socket.TryGetMessage(out message);
        }

        public bool TrySendMessage(Message message)
        {
            return socket.TrySendMessage(message);
        }

        public void Release( Message message)
        {
            socket.MessageFactory.Release(message);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected volatile bool isDisposed = false;
        private bool isFinalized;

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
                if (disposing)
                {
                    if (debug) log.DebugFormat("Dispose()");
                    if (socket != null)
                    {
                        socket.Dispose();
                    }
                    if (retryTimer != null)
                    {
                        retryTimer.Dispose();
                    }
                    isFinalized = true;
                }
            }
        }

        private Yield RetryTimerEvent()
        {
            log.Info("Connection Timeout");
            RetryDelay += retryIncrease;
            RetryDelay = Math.Min(RetryDelay, retryMaximum);
            Regenerate();
            return Yield.DidWork.Repeat;
        }

        private void OnConnect(Socket socket)
        {
            if (!this.socket.Equals(socket))
            {
                log.Info("OnConnect( " + this.socket + " != " + socket + " ) - Ignored.");
                return;
            }
            log.Info("OnConnect( " + socket + " ) ");
            retryTimer.Cancel();
            onConnect();
        }

        public void OnDisconnect(Socket socket)
        {
            if (!this.socket.Equals(socket))
            {
                log.Info("OnDisconnect( " + this.socket + " != " + socket + " ) - Ignored.");
                return;
            }
            log.Info("OnDisconnect( " + socket + " )  ");
            if (isDisposed)
            {
                isFinalized = true;
                log.Info("Socket was disposed. Now finalized: " + new StackTrace());
                return;
            }
            onDisconnect();
        }

        public long RetryDelay
        {
            get { return retryDelay; }
            set { retryDelay = value; }
        }

        public long RetryStart
        {
            get { return retryStart; }
            set { retryStart = value; }
        }

        public long RetryIncrease
        {
            get { return retryIncrease; }
            set { retryIncrease = value; }
        }

        public long RetryMaximum
        {
            get { return retryMaximum; }
            set { retryMaximum = value; }
        }

        public Socket Socket
        {
            get { return socket; }
        }

        public SocketState State
        {
            get { return socket.State; }
        }

        public void ResetRetry()
        {
            if (RetryDelay != RetryStart)
            {
                RetryDelay = RetryStart;
                log.Info("(retryDelay reset to " + RetryDelay + " seconds.)");
            }
        }

    }
}