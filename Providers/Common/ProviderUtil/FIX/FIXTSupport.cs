using System;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public abstract class FIXTSupport : AgentPerformer
    {
        protected readonly Log log;
        protected volatile bool trace;
        protected volatile bool debug;
        protected volatile bool verbose;
        protected readonly Log fixLog;
        protected volatile bool fixDebug;
        private long retryDelay = 30; // seconds
        private Socket socket;
        private volatile Status connectionStatus = Status.None;
        private volatile Status bestConnectionStatus = Status.None;
        private long retryIncrease = 5;
        private long retryMaximum = 30;
        private TimeStamp lastMessageTime;
        private long heartbeatDelay;
        private string addrStr;
        private ushort port;
        private long retryStart = 30; // seconds
        protected bool fastRetry = false;
        private string configSection;
        private bool isResendComplete = true;
        private string providerName;
        protected string name;

        public FIXTSupport(string name)
        {
            this.name = name;
            configSection = name;
            this.providerName = GetType().Name;
            log = Factory.SysLog.GetLogger(typeof(FIXProviderSupport) + "." + providerName + "." + name);
            log.Register(this);
            fixLog = Factory.SysLog.GetLogger(typeof(FIXProviderSupport).Namespace + ".FIXLog." + providerName + "." + name);
            fixLog.Register(this);
            verbose = log.IsVerboseEnabled;
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
            fixDebug = fixLog.IsDebugEnabled;
        }
        public enum Status
        {
            None,
            New,
            Connected,
            PendingLogin,
            PendingServerResend,
            PendingRecovery,
            Recovered,
            Disconnected,
            PendingRetry,
            PendingLogOut,
        }

        public virtual void RefreshLogLevel()
        {
            if (log != null)
            {
                debug = log.IsDebugEnabled;
                trace = log.IsTraceEnabled;
            }
            if (fixLog != null) fixDebug = fixLog.IsDebugEnabled;
        }
        private Task socketTask;
        private TrueTimer retryTimer;
        private Agent agent;
        private TrueTimer heartbeatTimer;
        private FastQueue<MessageFIXT1_1> resendQueue;
        public Agent Agent
        {
            get { return agent; }
            set { agent = value; }
        }

        protected abstract MessageFactory CreateMessageFactory();

        public void Initialize(Task task)
        {
            socketTask = task;
            socketTask.Scheduler = Scheduler.EarliestTime;
            retryTimer = Factory.Parallel.CreateTimer("Retry", socketTask, RetryTimerEvent);
            heartbeatTimer = Factory.Parallel.CreateTimer("Heartbeat", socketTask, HeartBeatTimerEvent);
            resendQueue = Factory.Parallel.FastQueue<MessageFIXT1_1>(providerName + "." + name + ".Resend");
            resendQueue.ConnectInbound(socketTask);
            socketTask.Start();
            if (debug) log.Debug("> SetupFolders.");
        }

        private Yield RetryTimerEvent()
        {
            log.Info("Connection Timeout");
            RetryDelay += retryIncrease;
            RetryDelay = Math.Min(RetryDelay, retryMaximum);
            SetupRetry();
            return Yield.DidWork.Repeat;
        }

        private int frozenHeartbeatCounter;
        private Yield HeartBeatTimerEvent()
        {
            var typeStr = ConnectionStatus == Status.PendingLogin ? "Login Timeout" : "Heartbeat timeout";
            log.Info(typeStr + ". Last Message UTC Time: " + lastMessageTime + ", current UTC Time: " + TimeStamp.UtcNow);
            log.Error("FIXProvider " + typeStr);
            if (SyncTicks.Frozen)
            {
                frozenHeartbeatCounter++;
                if (frozenHeartbeatCounter > 3)
                {
                    if (debug) log.Debug("More than 3 heart beats sent after frozen.  Ending heartbeats.");
                    HeartbeatDelay = 50;
                }
                else
                {
                    SetupRetry();
                }
            }
            else
            {
                SetupRetry();
            }
            IncreaseHeartbeatTimeout();
            return Yield.DidWork.Repeat;
        }

        protected void IncreaseHeartbeatTimeout()
        {
            var heartbeatTime = TimeStamp.UtcNow;
            if (SyncTicks.Enabled)
            {
                heartbeatTime.AddMilliseconds(heartbeatDelay * 100);
            }
            else
            {
                heartbeatTime.AddSeconds(heartbeatDelay);
            }
            heartbeatTimer.Start(heartbeatTime);
        }

        public long RetryDelay
        {
            get { return retryDelay; }
            set { retryDelay = value; }
        }

        private void SetupRetry()
        {
            orderStore.ForceSnapshot();
            OnRetry();
            RegenerateSocket();
        }
        public abstract void OnRetry();

        protected void RegenerateSocket()
        {
            Socket old = socket;
            if (socket != null && socket.State != SocketState.Closed)
            {
                socket.Dispose();
                // Wait for graceful socket shutdown.
                return;
            }
            socket = Factory.Provider.Socket(this.GetType().Name + "Socket", AddrStr, port);
            socket.ReceiveQueue.ConnectInbound(socketTask);
            socket.SendQueue.ConnectOutbound(socketTask);
            socket.OnConnect = OnClientConnect;
            socket.MessageFactory = CreateMessageFactory();
            if (debug) log.Debug("Created new " + socket);
            ConnectionStatus = Status.New;
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
                HeartbeatDelay = 10;
                if (HeartbeatDelay > 40)
                {
                    log.Error("Heartbeat delay is " + HeartbeatDelay);
                }
                RetryDelay = 1;
                RetryStart = 1;
            }
            else
            {
                HeartbeatDelay = 40;
                RetryDelay = 30;
            }
            // Initiate socket connection.
            while (true)
            {
                try
                {
                    socket.Connect();
                    if (debug) log.Debug("Requested Connect for " + socket);
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
                    return;
                }
                catch (SocketErrorException ex)
                {
                    log.Error("Non fatal error while trying to connect: " + ex.Message);
                }
            }
        }

        private void OnClientConnect(Socket socket)
        {
            if (!this.socket.Equals(socket))
            {
                log.Info("OnConnect( " + this.socket + " != " + socket + " ) - Ignored.");
                return;
            }
            log.Info("OnConnect( " + socket + " ) ");
            retryTimer.Cancel();
            ConnectionStatus = Status.Connected;
            IsResendComplete = true;
            using (OrderStore.BeginTransaction())
            {
                if (OnLogin())
                {
                    ConnectionStatus = Status.PendingLogin;
                    IncreaseHeartbeatTimeout();
                }
                else
                {
                    RegenerateSocket();
                }
            }
        }

        public Status ConnectionStatus
        {
            get { return connectionStatus; }
            set
            {
                if (connectionStatus != value)
                {
                    if (debug) log.Debug("ConnectionStatus changed from " + connectionStatus + " to " + value);
                    connectionStatus = value;
                }
            }
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

        public long HeartbeatDelay
        {
            get { return heartbeatDelay; }
            set
            {
                heartbeatDelay = value;
                IncreaseHeartbeatTimeout();
            }
        }

        public string AddrStr
        {
            get { return addrStr; }
            set { addrStr = value; }
        }

        public ushort Port
        {
            get { return port; }
            set { port = value; }
        }

        public long RetryStart
        {
            get { return retryStart; }
            set { retryStart = value; }
        }
        public bool IsResendComplete
        {
            get { return isResendComplete; }
            set
            {
                if (isResendComplete != value)
                {
                    if (debug) log.Debug("Resend Complete changed to " + value);
                    isResendComplete = value;
                }
            }
        }

        public string ProviderName
        {
            get { return providerName; }
            set { providerName = value; }
        }

    }
}