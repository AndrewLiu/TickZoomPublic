using System;
using System.Text;
using TickZoom.Api;
using TickZoom.TickUtil;

namespace TickZoom.Provider.FIX
{
    public class SimulateSymbolPlayback : SimulateSymbol, LogAware
    {
        private static Log log = Factory.SysLog.GetLogger(typeof(SimulateSymbolPlayback));
        private volatile bool debug;
        private volatile bool trace;
        public void RefreshLogLevel()
        {
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
        }
        private FillSimulator fillSimulator;
        private TickFile reader;
        private Action<Message, SymbolInfo, Tick> onTick;
        private Task queueTask;
        private SymbolInfo symbol;
        private TickIO nextTick = Factory.TickUtil.TickIO();
        private bool isFirstTick = true;
        private long playbackOffset;
        private FIXSimulatorSupport fixSimulatorSupport;
        private QuoteSimulatorSupport quoteSimulatorSupport;
        private LatencyMetric latency;
        private TrueTimer tickTimer;
        private long intervalTime = 1000000;
        private long prevTickTime;
        private bool isVolumeTest = false;
        private long tickCounter = 0;
        private int diagnoseMetric;
        private string symbolString;
        private Agent agent;
        public Agent Agent
        {
            get { return agent; }
            set { agent = value; }
        }

        public SimulateSymbolPlayback(FIXSimulatorSupport fixSimulatorSupport,
                                      QuoteSimulatorSupport quoteSimulatorSupport,
                                      string symbolString,
                                      Action<Message, SymbolInfo, Tick> onTick,
                                      Action<PhysicalFill,PhysicalOrder> onPhysicalFill,
                                      Action<PhysicalOrder, string> onRejectOrder)
        {
            log.Register(this);
            this.fixSimulatorSupport = fixSimulatorSupport;
            this.quoteSimulatorSupport = quoteSimulatorSupport;
            this.onTick = onTick;
            this.symbol = Factory.Symbol.LookupSymbol(symbolString);
            reader = Factory.TickUtil.TickFile();
            this.symbolString = symbolString;
            reader.Initialize("Test\\MockProviderData", symbolString, BinaryFileMode.Read);
            fillSimulator = Factory.Utility.FillSimulator("FIX", Symbol, false, true, null);
            fillSimulator.EnableSyncTicks = SyncTicks.Enabled;
            FillSimulator.OnPhysicalFill = onPhysicalFill;
            FillSimulator.OnRejectOrder = onRejectOrder;
        }

        public void Shutdown()
        {
            Dispose();
        }

        public void Initialize( Task task)
        {
            queueTask = task;
            queueTask.Name = "SimulateSymbolPlayback-" + symbolString;
            tickTimer = Factory.Parallel.CreateTimer("Tick", queueTask, PlayBackTick);
            queueTask.Scheduler = Scheduler.EarliestTime;
            quoteSimulatorSupport.QuotePacketQueue.ConnectOutbound(queueTask);
            queueTask.Start();
            latency = new LatencyMetric("SimulateSymbolPlayback-" + symbolString.StripInvalidPathChars());
            diagnoseMetric = Diagnose.RegisterMetric("Simulator");
        }

        public bool IsOnline
        {
            get { return FillSimulator.IsOnline; }
            set { fillSimulator.IsOnline = value; }
        }

        public int ActualPosition
        {
            get
            {
                return (int)FillSimulator.ActualPosition;
            }
        }

        public void CreateOrder(PhysicalOrder order)
        {
            FillSimulator.OnCreateBrokerOrder(order);
        }

        public void TryProcessAdjustments()
        {
            FillSimulator.ProcessAdjustments();
        }

        public bool ChangeOrder(PhysicalOrder order)
        {
            return FillSimulator.OnChangeBrokerOrder(order);
        }

        public void CancelOrder(PhysicalOrder order)
        {
            FillSimulator.OnCancelBrokerOrder(order);
        }

        public PhysicalOrder GetOrderById(long clientOrderId)
        {
            return FillSimulator.GetOrderById(clientOrderId);
        }

        public Yield Invoke()
        {
            LatencyManager.IncrementSymbolHandler();
            if (tickStatus == TickStatus.None || tickStatus == TickStatus.Sent)
            {
                return Yield.DidWork.Invoke(DequeueTick);
            }
            else
            {
                return Yield.NoWork.Repeat;
            }
        }

        private long GetNextUtcTime(long utcTime)
        {
            if (isVolumeTest)
            {
                return prevTickTime + intervalTime;
            }
            else
            {
                return utcTime + playbackOffset;
            }
        }

        public class ReadQueueEmptyException : Exception { }

        private TickIO currentTick = Factory.TickUtil.TickIO();
        private Yield DequeueTick()
        {
            LatencyManager.IncrementSymbolHandler();
            var result = Yield.NoWork.Repeat;

            if (isFirstTick)
            {
                if (!reader.TryReadTick(currentTick))
                {
                    return result;
                }
            }
            else
            {
                currentTick.Inject(nextTick.Extract());
                if (!reader.TryReadTick(nextTick))
                {
                    return result;
                }
            }
            tickCounter++;
            if (isFirstTick)
            {
                playbackOffset = fixSimulatorSupport.GetRealTimeOffset(currentTick.UtcTime.Internal);
                prevTickTime = TimeStamp.UtcNow.Internal + 5000000;
            }
            currentTick.SetTime(new TimeStamp(GetNextUtcTime(currentTick.lUtcTime)));
            prevTickTime = currentTick.UtcTime.Internal;
            if (tickCounter > 10)
            {
                intervalTime = 1000;
            }
            isFirstTick = false;
            FillSimulator.StartTick(currentTick);
            if (trace) log.TraceFormat(LogMessage.LOGMSG310, nextTick.UtcTime, nextTick.UtcTime.Microsecond);
            return Yield.DidWork.Invoke(ProcessTick);
        }

        public enum TickStatus
        {
            None,
            Timer,
            Sent,
        }

        private volatile TickStatus tickStatus = TickStatus.None;
        private Yield ProcessTick()
        {
            LatencyManager.IncrementSymbolHandler();
            var result = Yield.NoWork.Repeat;
            switch (tickStatus)
            {
                case TickStatus.None:
                    var overlapp = 300L;
                    var currentTime = TimeStamp.UtcNow;
                    if (tickTimer.Active) tickTimer.Cancel();
                    if (nextTick.UtcTime.Internal > currentTime.Internal + overlapp &&
                        tickTimer.Start(nextTick.UtcTime))
                    {
                        if (trace) log.TraceFormat(LogMessage.LOGMSG311, nextTick.UtcTime, nextTick.UtcTime.Microsecond, currentTime, currentTime.Microsecond);
                        tickStatus = TickStatus.Timer;
                    }
                    else
                    {
                        if (nextTick.UtcTime.Internal < currentTime.Internal)
                        {
                            if (trace)
                                log.TraceFormat(LogMessage.LOGMSG312, currentTime, nextTick.UtcTime, nextTick.UtcTime.Microsecond);
                            result = Yield.DidWork.Invoke(SendPlayBackTick);
                        }
                    }
                    break;
                case TickStatus.Sent:
                    result = Yield.DidWork.Invoke(Invoke);
                    break;
                case TickStatus.Timer:
                    break;
                default:
                    throw new ApplicationException("Unknown tick status: " + tickStatus);
            }
            return result;
        }

        private Yield SendPlayBackTick()
        {
            LatencyManager.IncrementSymbolHandler();
            latency.TryUpdate(nextTick.lSymbol, nextTick.UtcTime.Internal);
            if (isFirstTick)
            {
                FillSimulator.StartTick(nextTick);
                isFirstTick = false;
            }
            else
            {
                if( FillSimulator.IsChanged)
                {
                    FillSimulator.ProcessOrders();
                }
            }
            return Yield.DidWork.Invoke(ProcessOnTickCallBack);
        }

        private Message quoteMessage;
        private Yield ProcessOnTickCallBack()
        {
            LatencyManager.IncrementSymbolHandler();
            if (quoteMessage == null)
            {
                quoteMessage = quoteSimulatorSupport.QuoteSocket.MessageFactory.Create();
            }
            onTick(quoteMessage, Symbol, nextTick);
            if (trace) log.TraceFormat(LogMessage.LOGMSG147, nextTick.UtcTime);
            quoteMessage.SendUtcTime = nextTick.UtcTime.Internal;
            return Yield.DidWork.Invoke(TryEnqueuePacket);
        }

        private Yield TryEnqueuePacket()
        {
            LatencyManager.IncrementSymbolHandler();
            if (quoteMessage.Data.GetBuffer().Length == 0)
            {
                return Yield.NoWork.Return;
            }
            quoteSimulatorSupport.QuotePacketQueue.Enqueue(quoteMessage, quoteMessage.SendUtcTime);
            if (trace) log.TraceFormat(LogMessage.LOGMSG148, new TimeStamp(quoteMessage.SendUtcTime));
            quoteMessage = quoteSimulatorSupport.QuoteSocket.MessageFactory.Create();
            tickStatus = TickStatus.Sent;
            return Yield.DidWork.Return;
        }

        private Yield PlayBackTick()
        {
            var result = Yield.DidWork.Repeat;
            if (tickStatus == TickStatus.Timer)
            {
                if (trace) log.TraceFormat(LogMessage.LOGMSG313, nextTick.UtcTime);
                result = Yield.DidWork.Invoke(SendPlayBackTick);
            }
            return result;
        }

        private void OnException(Exception ex)
        {
            // Attempt to propagate the exception.
            log.Error("Exception occurred", ex);
            Dispose();
        }

        protected volatile bool isDisposed = false;
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
                    if (queueTask != null)
                    {
                        queueTask.Stop();
                        queueTask.Join();
                    }
                    if (reader != null)
                    {
                        reader.Dispose();
                    }
                    if( tickTimer != null)
                    {
                        tickTimer.Dispose();
                    }
                    if (fillSimulator != null)
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG314);
                        fillSimulator.IsOnline = false;
                    }
                    else
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG315);
                    }
                }
            }
            else
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG316, isDisposed);
            }
        }

        public FillSimulator FillSimulator
        {
            get { return fillSimulator; }
        }

        public SymbolInfo Symbol
        {
            get { return symbol; }
        }
    }
}