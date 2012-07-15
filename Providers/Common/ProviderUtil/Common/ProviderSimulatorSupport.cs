using System;
using System.Collections.Generic;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public class ProviderSimulatorSupport : AgentPerformer
    {
        private static Log log = Factory.SysLog.GetLogger(typeof(ProviderSimulatorSupport));
        private volatile bool debug;
        private volatile bool trace;
        private volatile bool verbose;
        public virtual void RefreshLogLevel()
        {
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
            verbose = log.IsVerboseEnabled;
        }

        private Agent agent;
        private Task task;
        private string mode;
        private SimpleLock symbolHandlersLocker = new SimpleLock();
        private Dictionary<long, SimulateSymbol> symbolHandlers = new Dictionary<long, SimulateSymbol>();
        private long nextSimulateSymbolId;
        private PartialFillSimulation partialFillSimulation;
        private TimeStamp endTime;
        private bool isOrderServerOnline = false;
        private FIXSimulatorSupport fixSimulator;
        private QuoteSimulatorSupport quotesSimulator;
        private QueueFilter filter;

        public ProviderSimulatorSupport(string mode, ProjectProperties projectProperties, Type fixSimulatorType, Type quoteSimulatorType)
        {
            this.mode = mode;
            partialFillSimulation = projectProperties.Simulator.PartialFillSimulation;
            this.endTime = projectProperties.Starter.EndTime;
            fixSimulator = (FIXSimulatorSupport) Factory.Parallel.SpawnPerformer(fixSimulatorType, mode, projectProperties, this);
            quotesSimulator = (QuoteSimulatorSupport) Factory.Parallel.SpawnPerformer(quoteSimulatorType, mode, projectProperties, this);
        }

        public void FlushFillQueues()
        {
            var handlers = new List<SimulateSymbol>();
            using (symbolHandlersLocker.Using())
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG303);
                foreach (var kvp in symbolHandlers)
                {
                    handlers.Add(kvp.Value);
                }
            }
            foreach (var handler in handlers)
            {
                handler.FillSimulator.FlushFillQueue();
            }
            if (debug) log.DebugFormat(LogMessage.LOGMSG304);
            foreach (var handler in handlers)
            {
                handler.FillSimulator.LogActiveOrders();
            }
        }

        public void SwitchBrokerState(string description, bool isOnline)
        {
            foreach (var kvp in symbolHandlers)
            {
                var symbolBinary = kvp.Key;
                var handler = kvp.Value;
                var tickSync = SyncTicks.GetTickSync(symbolBinary);
                if (handler.IsOnline != isOnline)
                {
                    tickSync.SetSwitchBrokerState(description);
                    handler.IsOnline = isOnline;
                    if (!isOnline)
                    {
                        while (tickSync.SentPhyscialOrders)
                        {
                            tickSync.RemovePhysicalOrder("Rollback");
                        }
                        while (tickSync.SentOrderChange)
                        {
                            tickSync.RemoveOrderChange();
                        }
                        while (tickSync.SentPositionChange)
                        {
                            tickSync.RemovePositionChange("Rollback");
                        }
                        while (tickSync.SentWaitingMatch)
                        {
                            tickSync.RemoveWaitingMatch("Rollback");
                        }
                    }
                }
            }
        }

        public void AddSymbol(string expandedSymbol)
        {
            var symbolInfo = Factory.Symbol.LookupSymbol(expandedSymbol);
            var symbol = symbolInfo.BaseSymbol;
            using (symbolHandlersLocker.Using())
            {
                if (!symbolHandlers.ContainsKey(symbolInfo.BinaryIdentifier))
                {
                    if (SyncTicks.Enabled)
                    {
                        var symbolHandler = (SimulateSymbol)Factory.Parallel.SpawnPerformer(typeof(SimulateSymbolSyncTicks),
                                                                                            fixSimulator, quotesSimulator, symbol, partialFillSimulation, endTime, nextSimulateSymbolId++);
                        symbolHandlers.Add(symbolInfo.BinaryIdentifier, symbolHandler);
                    }
                    else
                    {
                        var symbolHandler = (SimulateSymbol)Factory.Parallel.SpawnPerformer(typeof(SimulateSymbolRealTime),
                                                                                            fixSimulator, quotesSimulator, symbol, partialFillSimulation, nextSimulateSymbolId++);
                        symbolHandlers.Add(symbolInfo.BinaryIdentifier, symbolHandler);
                    }
                }
            }
            if (IsOrderServerOnline)
            {
                SetOrderServerOnline();
            }
        }

        public void SetOrderServerOnline()
        {
            using (symbolHandlersLocker.Using())
            {
                foreach (var kvp in symbolHandlers)
                {
                    var handler = kvp.Value;
                    handler.IsOnline = true;
                }
            }
            if (!IsOrderServerOnline)
            {
                IsOrderServerOnline = true;
                log.Info("Order server back online.");
            }
        }

        public void SetOrderServerOffline()
        {
            using (symbolHandlersLocker.Using())
            {
                foreach (var kvp in symbolHandlers)
                {
                    var handler = kvp.Value;
                    handler.IsOnline = false;
                }
            }
            IsOrderServerOnline = false;
        }

        public int GetPosition(SymbolInfo symbol)
        {
            // Don't lock. This call always wrapped in a locked using clause.
            SimulateSymbol symbolSyncTicks;
            if (symbolHandlers.TryGetValue(symbol.BinaryIdentifier, out symbolSyncTicks))
            {
                return symbolSyncTicks.ActualPosition;
            }
            return 0;
        }

        private Dictionary<long, PhysicalOrder> orders = new Dictionary<long, PhysicalOrder>();
        public void CreateOrder(PhysicalOrder order)
        {
            SimulateSymbol symbolSyncTicks;
            using (symbolHandlersLocker.Using())
            {
                symbolHandlers.TryGetValue(order.Symbol.BinaryIdentifier, out symbolSyncTicks);
            }
            if (symbolSyncTicks != null)
            {
                symbolSyncTicks.CreateOrder(order);
                orders.Add(order.BrokerOrder,order);
            }
        }

        public void TryProcessAdustments(PhysicalOrder order)
        {
            SimulateSymbol symbolSyncTicks;
            using (symbolHandlersLocker.Using())
            {
                symbolHandlers.TryGetValue(order.Symbol.BinaryIdentifier, out symbolSyncTicks);
            }
            if (symbolSyncTicks != null)
            {
                symbolSyncTicks.TryProcessAdjustments();
            }
        }

        public void ChangeOrder(PhysicalOrder order)
        {
            SimulateSymbol symbolSyncTicks;
            using (symbolHandlersLocker.Using())
            {
                symbolHandlers.TryGetValue(order.Symbol.BinaryIdentifier, out symbolSyncTicks);
            }
            if (symbolSyncTicks != null)
            {
                orders.Remove(order.OriginalOrder.BrokerOrder);
                if( symbolSyncTicks.ChangeOrder(order))
                {
                    orders.Add(order.BrokerOrder, order);
                }
            }
        }

        public void CancelOrder(PhysicalOrder order)
        {
            SimulateSymbol symbolSyncTicks;
            using (symbolHandlersLocker.Using())
            {
                symbolHandlers.TryGetValue(order.Symbol.BinaryIdentifier, out symbolSyncTicks);
            }
            if (symbolSyncTicks != null)
            {
                symbolSyncTicks.CancelOrder(order);
                orders.Remove(order.OriginalOrder.BrokerOrder);
            }
        }

        public bool TryGetOrderById(long clientOrderId, out PhysicalOrder order)
        {
            return orders.TryGetValue(clientOrderId, out order);
        }

        public PhysicalOrder GetOrderById(long clientOrderId)
        {
            PhysicalOrder order;
            if( !orders.TryGetValue(clientOrderId, out order))
            {
                throw new ApplicationException("Cannot find client order by id: " + clientOrderId);
            }
            return order;
        }

        public void Shutdown()
        {
            Dispose();
        }

        public int Count
        {
            get { return symbolHandlers.Count; }
        }

        public bool IsOrderServerOnline
        {
            get { return isOrderServerOnline; }
            set { isOrderServerOnline = value; }
        }

        public long NextSimulateSymbolId
        {
            get { return nextSimulateSymbolId; }
        }

        public Agent Agent
        {
            get { return agent; }
            set { agent = value; }
        }

        public void Initialize(Task task)
        {
            this.task = task;
            filter = task.GetFilter();
            task.Scheduler = Scheduler.EarliestTime;
            task.Start();
            if (debug) log.DebugFormat(LogMessage.LOGMSG305);
        }

        public Yield Invoke()
        {
            return Yield.NoWork.Repeat;
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
                    if( fixSimulator != null)
                    {
                        fixSimulator.Dispose();
                    }
                    if( quotesSimulator != null)
                    {
                        quotesSimulator.Dispose();
                    }
                    if (debug) log.DebugFormat(LogMessage.LOGMSG306);
                    if (symbolHandlers != null)
                    {
                        using (symbolHandlersLocker.Using())
                        {
                            if (debug) log.DebugFormat(LogMessage.LOGMSG307, symbolHandlers.Count);
                            foreach (var kvp in symbolHandlers)
                            {
                                var handler = kvp.Value;
                                if (debug) log.DebugFormat(LogMessage.LOGMSG308, handler);
                                handler.Agent.SendEvent(new EventItem(EventType.Shutdown));
                            }
                            symbolHandlers.Clear();
                        }
                    }
                    else
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG309);
                    }
                    if (task != null)
                    {
                        task.Stop();
                    }
                }
            }
        }
    }
}