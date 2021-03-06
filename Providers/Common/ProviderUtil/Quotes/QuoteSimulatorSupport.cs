using System;
using System.Text;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public abstract class QuoteSimulatorSupport : LogAware, AgentPerformer
    {
        private static Log log = Factory.SysLog.GetLogger(typeof(QuoteSimulatorSupport));
        private volatile bool debug;
        private volatile bool trace;
        private volatile bool verbose;
        public virtual void RefreshLogLevel()
        {
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
            verbose = log.IsVerboseEnabled;
        }
        private Task task;
        private Agent agent;
        private string localAddress = "0.0.0.0";
        private ushort quotesPort = 0;
        private Socket quoteListener;
        private Socket quoteSocket;
        private Message _quoteReadMessage;
        private Message _quoteWriteMessage;
        private bool isQuoteSimulationStarted = false;
        private MessageFactory _quoteMessageFactory;
        private FastQueue<Message> quotePacketQueue = Factory.Parallel.FastQueue<Message>("SimulatorQuote");
        private QueueFilter filter;
        private ProviderSimulatorSupport providerSimulator;

        public QuoteSimulatorSupport(ProviderSimulatorSupport providerSimulator, ushort quotesPort, MessageFactory _quoteMessageFactory)
        {
            log.Register(this);
            this.providerSimulator = providerSimulator;
            this.quotesPort = quotesPort;
            this._quoteMessageFactory = _quoteMessageFactory;
        }

        public void Initialize(Task task)
        {
            this.task = task;
            filter = task.GetFilter();
            task.Scheduler = Scheduler.EarliestTime;
            quotePacketQueue.ConnectInbound(task);
            task.Start();
            ListenToQuotes();
            if (debug) log.DebugFormat(LogMessage.LOGMSG180);
        }

        private enum State { Start, ProcessQuotes, WriteQuotes, Return };
        private State state = State.Start;
        private bool hasQuotePacket = false;
        public Yield Invoke()
        {
            var result = false;
            switch (state)
            {
                case State.Start:
                    if (QuotesReadLoop())
                    {
                        result = true;
                    }
                    ProcessQuotes:
                    hasQuotePacket = ProcessQuotePackets();
                    if (hasQuotePacket)
                    {
                        result = true;
                    }
                    WriteQuotes:
                    if (hasQuotePacket)
                    {
                        if (!WriteToQuotes())
                        {
                            state = State.WriteQuotes;
                            return Yield.NoWork.Repeat;
                        }
                    }
                    break;
                case State.WriteQuotes:
                    goto WriteQuotes;
                case State.ProcessQuotes:
                    goto ProcessQuotes;
            }
            state = State.Start;
            if (result)
            {
                return Yield.DidWork.Repeat;
            }
            else
            {
                return Yield.NoWork.Repeat;
            }
        }

        private void ListenToQuotes()
        {
            quoteListener = Factory.Provider.Socket(typeof(FIXSimulatorSupport).Name, localAddress, quotesPort);
            quoteListener.Bind();
            quoteListener.Listen(5);
            quoteListener.OnConnect = OnConnectQuotes;
            quotesPort = quoteListener.Port;
            log.Info("Listening for Quotes to " + localAddress + " on port " + quotesPort);
        }

        protected virtual void OnConnectQuotes(Socket socket)
        {
            quoteSocket = socket;
            quoteSocket.MessageFactory = _quoteMessageFactory;
            log.Info("Received quotes connection: " + socket);
            StartQuoteSimulation();
            quoteSocket.ReceiveQueue.ConnectInbound(task);
            quoteSocket.SendQueue.ConnectOutbound(task);
        }

        public void StartQuoteSimulation()
        {
            isQuoteSimulationStarted = true;
        }

        private void OnDisconnectQuotes(Socket socket)
        {
            if (this.quoteSocket.Equals(socket))
            {
                log.Info("Quotes socket disconnect: " + socket);
                CloseQuoteSocket();
            }
        }

        protected void CloseQuoteSocket()
        {
            if (quotePacketQueue != null)
            {
                quotePacketQueue.Clear();
            }
            if (quoteSocket != null)
            {
                quoteSocket.Dispose();
            }
        }

        private bool ProcessQuotePackets()
        {
            if (_quoteWriteMessage == null && quotePacketQueue.Count == 0)
            {
                return false;
            }
            if (trace) log.TraceFormat(LogMessage.LOGMSG181, quotePacketQueue.Count);
            if (quotePacketQueue.TryDequeue(out _quoteWriteMessage))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        protected void CloseSockets()
        {
            if (task != null)
            {
                task.Stop();
                task.Join();
            }
            CloseQuoteSocket();
        }

        private bool QuotesReadLoop()
        {
            if (isQuoteSimulationStarted)
            {
                Message message;
                if (quoteSocket.TryGetMessage(out message))
                {
                    var disconnect = message as DisconnectMessage;
                    if (disconnect == null)
                    {
                        _quoteReadMessage = message;
                        if (verbose) log.VerboseFormat(LogMessage.LOGMSG182, _quoteReadMessage);
                        ParseQuotesMessage(_quoteReadMessage);
                        quoteSocket.MessageFactory.Release(_quoteReadMessage);
                        return true;
                    }
                    OnDisconnectQuotes(disconnect.Socket);
                }
            }
            return false;
        }

        protected virtual void ParseQuotesMessage(Message quoteReadMessage)
        {
            throw new NotImplementedException();
        }


        public bool WriteToQuotes()
        {
            if (!isQuoteSimulationStarted || _quoteWriteMessage == null) return true;
            if (quoteSocket.TrySendMessage(_quoteWriteMessage))
            {
                if (trace) log.TraceFormat(LogMessage.LOGMSG144, _quoteWriteMessage);
                _quoteWriteMessage = null;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Shutdown()
        {
            Dispose();
        }

        protected volatile bool isDisposed = false;
        protected TickIO[] lastTicks = new TickIO[0];
        protected CurrentTick[] currentTicks = new CurrentTick[0];

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
                    if (debug) log.DebugFormat(LogMessage.LOGMSG48);
                    CloseSockets();
                    if (quoteListener != null)
                    {
                        quoteListener.Dispose();
                    }
                    if (task != null)
                    {
                        task.Stop();
                    }
                }
            }
        }

        public ushort QuotesPort
        {
            get { return quotesPort; }
        }

        public Socket QuoteSocket
        {
            get { return quoteSocket; }
        }

        public FastQueue<Message> QuotePacketQueue
        {
            get { return quotePacketQueue; }
        }

        public ProviderSimulatorSupport ProviderSimulator
        {
            get { return providerSimulator; }
        }

        public Agent Agent
        {
            get { return agent; }
            set { agent = value; }
        }

        public enum TickState
        {
            None,
            Tick,
            Finish,
        }

        protected class CurrentTick
        {

            public TickState State;
            public SymbolInfo Symbol;
            public TickIO TickIO = Factory.TickUtil.TickIO();
        }

        protected void ExtendLastTicks()
        {
            var length = lastTicks.Length == 0 ? 256 : lastTicks.Length * 2;
            Array.Resize(ref lastTicks, length);
            for (var i = 0; i < lastTicks.Length; i++)
            {
                if (lastTicks[i] == null)
                {
                    lastTicks[i] = Factory.TickUtil.TickIO();
                }
            }
        }

        protected void ExtendCurrentTicks()
        {
            var length = currentTicks.Length == 0 ? 256 : currentTicks.Length * 2;
            Array.Resize(ref currentTicks, length);
            for (var i = 0; i < currentTicks.Length; i++)
            {
                if (currentTicks[i] == null)
                {
                    currentTicks[i] = new CurrentTick();
                }
            }
        }

        protected abstract void TrySendTick(SymbolInfo symbol, TickIO tick);

        public void OnTick(long id, SymbolInfo anotherSymbol, Tick anotherTick)
        {
            if (trace) log.TraceFormat(LogMessage.LOGMSG183, anotherTick);

            if (anotherSymbol.BinaryIdentifier >= lastTicks.Length)
            {
                ExtendLastTicks();
            }

            if (ProviderSimulator.NextSimulateSymbolId >= currentTicks.Length)
            {
                ExtendCurrentTicks();
            }

            var currentTick = currentTicks[id];
            currentTick.TickIO.Inject(anotherTick.Extract());
            currentTick.Symbol = anotherSymbol;
            currentTick.State = TickState.Tick;

            TryFindTick();
        }

        private void TryFindTick()
        {
            CurrentTick currentTick = null;
            for (var i = 0; i < ProviderSimulator.NextSimulateSymbolId; i++)
            {
                var temp = currentTicks[i];
                switch (temp.State)
                {
                    case TickState.None:
                        return;
                    case TickState.Tick:
                        if (currentTick == null || temp.TickIO.lUtcTime < currentTick.TickIO.lUtcTime)
                        {
                            currentTick = temp;
                        }
                        break;
                    case TickState.Finish:
                        break;
                }
            }
            if (currentTick == null) return;
            currentTick.State = TickState.None;
            var tick = currentTick.TickIO;
            var symbol = currentTick.Symbol;
            TrySendTick(symbol, tick);
        }

        public virtual void OnEndTick(long id)
        {
            if (ProviderSimulator.NextSimulateSymbolId >= currentTicks.Length)
            {
                ExtendCurrentTicks();
            }
            var currentTick = currentTicks[id];
            currentTick.State = TickState.Finish;
            TryFindTick();
        }
    }
}