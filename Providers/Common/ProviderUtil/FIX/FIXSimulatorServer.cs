using System;
using System.Collections.Generic;
using System.Text;
using TickZoom.Api;

namespace TickZoom.FIX
{
    public abstract class FIXSimulatorServer : LogAware
    {
        private string localAddress = "0.0.0.0";
        private static Log log = Factory.SysLog.GetLogger(typeof(FIXSimulatorSupport));
        private volatile bool debug;
        private volatile bool trace;
        private volatile bool verbose;
        public virtual void RefreshLogLevel()
        {
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
            verbose = log.IsVerboseEnabled;
        }

        protected FIXTFactory1_1 fixFactory;

        // FIX fields.
        private ushort fixPort = 0;
        private Socket fixListener;
        private Task task;
        private QueueFilter filter;

        private Agent agent;
        public Agent Agent
        {
            get { return agent; }
            set { agent = value; }
        }

        public FIXSimulatorServer(string mode, ProjectProperties projectProperties, ProviderSimulatorSupport providerSimulator, ushort fixPort, MessageFactory createMessageFactory)
        {
            this.fixPort = fixPort;
            this.providerSimulator = providerSimulator;
            var randomSeed = new Random().Next(int.MaxValue);
            if (heartbeatDelay > 1)
            {
                log.Error("Heartbeat delay is " + heartbeatDelay);
            }

            if (randomSeed != 1234)
            {
                Console.WriteLine("Random seed for fix simulator:" + randomSeed);
                log.Info("Random seed for fix simulator:" + randomSeed);
            }
            log.Register(this);
            switch (mode)
            {
                case "PlayBack":
                    break;
                default:
                    break;
            }

            this.currentMessageFactory = createMessageFactory;
        }

        public void Initialize(Task task)
        {
            this.task = task;
            filter = task.GetFilter();
            task.Scheduler = Scheduler.EarliestTime;
            task.Start();
            ListenToFIX();
            if (debug) log.Debug("Starting FIX Simulator.");
        }

        private void ListenToFIX()
        {
            fixListener = Factory.Provider.Socket(typeof(FIXSimulatorSupport).Name, localAddress, fixPort);
            fixListener.Bind();
            fixListener.Listen(5);
            fixListener.OnConnect = OnConnectFIX;
            fixPort = fixListener.Port;
            log.Info("Listening for FIX to " + localAddress + " on port " + fixPort);
        }

        protected virtual void OnConnectFIX(Socket socket)
        {
            fixSocket = socket;
            fixState = ServerState.Startup;
            fixSocket.MessageFactory = currentMessageFactory;
            log.Info("Received FIX connection: " + socket);
            StartFIXSimulation();
            fixSocket.ReceiveQueue.ConnectInbound(task);
            fixSocket.SendQueue.ConnectOutbound(task);
        }

        protected void CloseSockets()
        {
            if (task != null)
            {
                task.Stop();
                task.Join();
            }
        }

        public void Shutdown()
        {
            Dispose();
        }

        public Yield Invoke()
        {
            var result = false;
            if (result)
            {
                return Yield.DidWork.Repeat;
            }
            else
            {
                return Yield.NoWork.Repeat;
            }
        }


        protected virtual FIXTFactory1_1 CreateFIXFactory(int sequence, string target, string sender)
        {
            throw new NotImplementedException();
        }


        protected volatile bool isDisposed = false;
        private int heartbeatResponseTimeoutSeconds = 15;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
                if (disposing)
                {
                    if (debug) log.Debug("Dispose()");
                    var sb = new StringBuilder();
                    CloseSockets();
                    if (fixListener != null)
                    {
                        fixListener.Dispose();
                    }
                    if (task != null)
                    {
                        task.Stop();
                    }
                }
            }
        }

        public ushort FIXPort
        {
            get { return fixPort; }
        }
        public FIXTFactory1_1 FixFactory
        {
            get { return fixFactory; }
        }

    }
}