using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using TickZoom.Api;

namespace TickZoom.Common
{
    public class PhysicalOrderCacheDefault : PhysicalOrderCache, LogAware
    {
        private Log log;
        private volatile bool info;
        private volatile bool trace;
        private volatile bool debug;
        public virtual void RefreshLogLevel()
        {
            if( log != null)
            {
                info = log.IsInfoEnabled;
                debug = log.IsDebugEnabled;
                trace = log.IsTraceEnabled;
            }
        }

        protected Dictionary<int, PhysicalOrder> ordersBySequence = new Dictionary<int, PhysicalOrder>();
        protected Dictionary<long, PhysicalOrder> ordersByBrokerId = new Dictionary<long, PhysicalOrder>();
        protected Dictionary<long, PhysicalOrder> ordersBySerial = new Dictionary<long, PhysicalOrder>();
        protected Dictionary<long, SymbolPosition> positions = new Dictionary<long, SymbolPosition>();
        protected Dictionary<int, StrategyPosition> strategyPositions = new Dictionary<int, StrategyPosition>();
        private Func<PhysicalOrder,bool> matchSymbolFunc;
        private Pool<PhysicalOrderDefault> orderPool = Factory.Parallel.Pool<PhysicalOrderDefault>();
        private string name;
        private int callerId;

        protected class SymbolPosition
        {
            public long Position;
            public override string ToString()
            {
                return Position.ToString();
            }
        }

        public PhysicalOrderCacheDefault(string name)
        {
            this.name = name;
            log = Factory.SysLog.GetLogger(typeof(PhysicalOrderCacheDefault).FullName + "." + name);
            matchSymbolFunc = MatchSymbol;
            log.Register(this);
            callerId = orderPool.GetCallerId("PhysicalOrderCache");
        }

        public bool MatchSymbol(PhysicalOrder order)
        {
            return order.Symbol.BinaryIdentifier == temporarySymbol.BinaryIdentifier;
        }

        public virtual void AssertAtomic() { }
        private SymbolInfo temporarySymbol;
        private const int sortTimesCount = 64;
        private PhysicalOrder[] sortTimesArray = new PhysicalOrder[sortTimesCount];
        private List<PhysicalOrder> tempActiveOrders = new List<PhysicalOrder>();
        public void GetActiveOrders(List<PhysicalOrder> orders, SymbolInfo symbol)
        {
            orders.Clear();
            if (debug) log.DebugFormat(LogMessage.LOGMSG533, symbol);
            AssertAtomic();
            Array.Clear(sortTimesArray, 0, sortTimesArray.Length);
            var excludedCount = 0;
            temporarySymbol = symbol;
            GetOrders(tempActiveOrders, matchSymbolFunc);

            foreach (var order in tempActiveOrders)
            {
                if (order.OrderState != OrderState.Filled)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG534, order);
                    orders.Add(order);
                }
                else
                {
                    if( excludedCount < sortTimesArray.Length)
                    {
                        sortTimesArray[excludedCount] = order;
                    }
                    ++excludedCount;
                }
            }
            if( excludedCount >= sortTimesCount >> 1)
            {
                if( debug) log.DebugFormat("Found {0} filled orders.", excludedCount);
                Array.Sort(sortTimesArray, OnComparison);
                if (trace) log.TraceFormat("Sorted orders by last modify time:");
                var count = 0;
                for (var x = 0; x < sortTimesArray.Length; x++)
                {
                    var order = sortTimesArray[x];
                    if (order != null)
                    {
                        if (count >= excludedCount >> 1)
                        {
                            if (trace) log.TraceFormat("Removing order: {0}", sortTimesArray[x]);
                            RemoveOrderInternal(order.BrokerOrder);
                        }
                        else
                        {
                            if (trace) log.TraceFormat("Keeping order: {0}", sortTimesArray[x]);
                        }
                        count++;
                    }
                }
            }
        }

        private int OnComparison(PhysicalOrder x, PhysicalOrder y)
        {
            return (int) ((y == null ? 0L : y.LastModifyTime.Internal) - (x == null ? 0L : x.LastModifyTime.Internal));
        }

        public void SyncPositions(Iterable<StrategyPosition> strategyPositions)
        {
            if( strategyPositions == null) return;
            if (trace)
            {
                for (var node = strategyPositions.First; node != null; node = node.Next)
                {
                    var sp = node.Value;
                    log.TraceFormat(LogMessage.LOGMSG536, sp);
                }
            }
            for (var current = strategyPositions.First; current != null; current = current.Next)
            {
                var position = current.Value;
                StrategyPosition strategyPosition;
                if (!this.strategyPositions.TryGetValue(position.Id, out strategyPosition))
                {
                    strategyPosition = new StrategyPositionDefault(position.Id, position.Symbol);
                    this.strategyPositions.Add(position.Id, strategyPosition);
                }
                strategyPosition.TrySetPosition(position.ExpectedPosition);
            }
//            if( debug) log.Debug("SyncPositions() strategy positions:\n" + StrategyPositionsToString());
        }

        public void SetActualPosition(SymbolInfo symbol, long position)
        {
            using (positionsLocker.Using())
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG537, symbol, position);
                SymbolPosition symbolPosition;
                if (!positions.TryGetValue(symbol.BinaryIdentifier, out symbolPosition))
                {
                    symbolPosition = new SymbolPosition { Position = position };
                    positions.Add(symbol.BinaryIdentifier, symbolPosition);
                }
                else
                {
                    positions[symbol.BinaryIdentifier].Position = position;
                }
            }
        }

        public long GetActualPosition(SymbolInfo symbol)
        {
            using (positionsLocker.Using())
            {
                SymbolPosition symbolPosition;
                if (positions.TryGetValue(symbol.BinaryIdentifier, out symbolPosition))
                {
                    return symbolPosition.Position;
                }
                else
                {
                    return 0L;
                }
            }
        }

        public void SetStrategyPosition(SymbolInfo symbol, int strategyId, long position)
        {
            using (positionsLocker.Using())
            {
                StrategyPosition strategyPosition;
                if (!this.strategyPositions.TryGetValue(strategyId, out strategyPosition))
                {
                    strategyPosition = new StrategyPositionDefault(strategyId, symbol);
                    this.strategyPositions.Add(strategyId, strategyPosition);
                }
                strategyPosition.SetExpectedPosition(position);
            }
//            if (debug) log.Debug("SetStrategyPosition() strategy positions:\n" + StrategyPositionsToString());
        }

        public long GetStrategyPosition(int strategyId)
        {
            using (positionsLocker.Using())
            {
                StrategyPosition strategyPosition;
                if (strategyPositions.TryGetValue(strategyId, out strategyPosition))
                {
                    return strategyPosition.ExpectedPosition;
                }
                else
                {
                    return 0L;
                }
            }
        }

        public long IncreaseActualPosition(SymbolInfo symbol, long increase)
        {
            using (positionsLocker.Using())
            {
                SymbolPosition symbolPosition;
                if (!positions.TryGetValue(symbol.BinaryIdentifier, out symbolPosition))
                {
                    symbolPosition = new SymbolPosition { Position = increase };
                    positions.Add(symbol.BinaryIdentifier, symbolPosition);
                }
                else
                {
                    symbolPosition.Position += increase;
                }
                return symbolPosition.Position;
            }
        }

        public bool TryGetOrderById(long brokerOrder, out PhysicalOrder order)
        {
            AssertAtomic();
            return ordersByBrokerId.TryGetValue(brokerOrder, out order);
        }

        public bool TryGetOrderBySequence(int sequence, out PhysicalOrder order)
        {
            AssertAtomic();
            if (sequence == 0)
            {
                order = null;
                return false;
            }
            return ordersBySequence.TryGetValue(sequence, out order);
        }

        public PhysicalOrder GetOrderById(long brokerOrder)
        {
            AssertAtomic();
            PhysicalOrder order;
            if (!ordersByBrokerId.TryGetValue(brokerOrder, out order))
            {
                throw new ApplicationException("Unable to find order for id: " + brokerOrder);
            }
            return order;
        }

        public void PurgeOriginalOrder(PhysicalOrder order)
        {
            if( order.OriginalOrder == null) return;
            var clientOrderId = order.OriginalOrder.BrokerOrder;
            if (trace) log.TraceFormat(LogMessage.LOGMSG538, clientOrderId);
            AssertAtomic();
            RemoveOrderInternal(order.OriginalOrder.BrokerOrder);
            order.OriginalOrder = null;
        }

        public PhysicalOrderDefault Create()
        {
            return orderPool.Create(callerId);
        }


        public void RemoveOrder(long clientOrderId)
        {
            if (trace) log.TraceFormat(LogMessage.LOGMSG539, clientOrderId);
            AssertAtomic();
            var topOrder = RemoveOrderInternal(clientOrderId);
            if( topOrder != null && topOrder.OriginalOrder != null)
            {
                topOrder.OriginalOrder.ReplacedBy = null;
            }
        }

        private PhysicalOrder RemoveOrderInternal(long clientOrderId)
        {
            if (clientOrderId == 0)
            {
                return null;
            }
            PhysicalOrder order = null;
            if (ordersByBrokerId.TryGetValue(clientOrderId, out order))
            {
                var result = ordersByBrokerId.Remove(clientOrderId);
                if (result && trace) log.TraceFormat(LogMessage.LOGMSG540, clientOrderId, order);
                PhysicalOrder orderBySerial;
                if (ordersBySerial.TryGetValue(order.LogicalSerialNumber, out orderBySerial))
                {
                    if (orderBySerial.BrokerOrder.Equals(clientOrderId))
                    {
                        var result2 = ordersBySerial.Remove(order.LogicalSerialNumber);
                        if (result2 && trace) log.TraceFormat(LogMessage.LOGMSG541, order.LogicalSerialNumber, orderBySerial);
                    }
                }
                ordersBySequence.Remove(order.Sequence);
                orderPool.Free((PhysicalOrderDefault)order);
                return order;
            }
            return null;
        }

        public bool TryGetOrderBySerial(long logicalSerialNumber, out PhysicalOrder order)
        {
            AssertAtomic();
            return ordersBySerial.TryGetValue(logicalSerialNumber, out order);
        }

        public PhysicalOrder GetOrderBySerial(long logicalSerialNumber)
        {
            AssertAtomic();
            PhysicalOrder order;
            if (!ordersBySerial.TryGetValue(logicalSerialNumber, out order))
            {
                throw new ApplicationException("Unable to find order by serial for id: " + logicalSerialNumber);
            }
            return order;
        }

        private List<PhysicalOrder> tempCreateOrders = new List<PhysicalOrder>();
        public bool HasCreateOrder(PhysicalOrder order)
        {
            GetActiveOrders(tempCreateOrders, order.Symbol);
            foreach (var queueOrder in tempCreateOrders)
            {
                if (queueOrder.Action == OrderAction.Create && order.LogicalSerialNumber == queueOrder.LogicalSerialNumber)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG542, queueOrder);
                    return true;
                }
            }
            return false;
        }

        private List<PhysicalOrder> tempCancelOrders = new List<PhysicalOrder>();
        public bool HasCancelOrder(PhysicalOrder order)
        {
            GetActiveOrders(tempCancelOrders, order.Symbol);
            foreach (var clientId in tempCancelOrders)
            {
                if (clientId.OriginalOrder != null && order.OriginalOrder.BrokerOrder == clientId.OriginalOrder.BrokerOrder)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG543, order);
                    return true;
                }
            }
            return false;
        }

        public void SetOrder(PhysicalOrder order)
        {
            AssertAtomic();
            if (trace) log.TraceFormat(LogMessage.LOGMSG544, order.BrokerOrder, order.LogicalSerialNumber);
            ordersByBrokerId[order.BrokerOrder] = order;
            if (order.Sequence != 0)
            {
                ordersBySequence[order.Sequence] = order;
            }
            if (order.LogicalSerialNumber != 0)
            {
                ordersBySerial[order.LogicalSerialNumber] = order;
                if (order.Action == OrderAction.Cancel && order.OriginalOrder == null)
                {
                    throw new ApplicationException("CancelOrder w/o any original order setting: " + order);
                }
            }
        }

        public List<PhysicalOrder> GetOrdersList(Func<PhysicalOrder, bool> select)
        {
            var list = new List<PhysicalOrder>();
            GetOrders(list, select);
            return list;
        }

        public void GetOrders(List<PhysicalOrder> orders, Func<PhysicalOrder, bool> select)
        {
            AssertAtomic();
            orders.Clear();
            foreach (var kvp in ordersByBrokerId)
            {
                var order = kvp.Value;
                if (select(order))
                {
                    orders.Add(order);
                }
            }
        }

        public void ResetLastChange()
        {
            AssertAtomic();
            if (debug) log.DebugFormat(LogMessage.LOGMSG545);
            foreach (var kvp in ordersByBrokerId)
            {
                var order = kvp.Value;
                order.ResetLastChange();
            }
        }

        public int GetHighestSequence()
        {
            var highestSequence = 0;
            foreach (var kvp in ordersByBrokerId)
            {
                var order = kvp.Value;
                if( order.Sequence > highestSequence)
                {
                    highestSequence = order.Sequence;
                }
            }
            return highestSequence;
        }

        public void LogOrders(Log log)
        {
            foreach (var kvp in ordersByBrokerId)
            {
                log.DebugFormat(LogMessage.LOGMSG611,kvp.Value);
            }
        }

        private volatile bool isDisposed = false;
        protected SimpleLock positionsLocker = new SimpleLock();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                log.Info("Dispose()");
                if (disposing)
                {
                    isDisposed = true;
                }
            }
        }

        public int Count()
        {
            return ordersByBrokerId.Count;
        }

        public string SymbolPositionsToString()
        {
            using (positionsLocker.Using())
            {
                return SymbolPositionsToStringInternal();
            }
        }

        public string StrategyPositionsToString()
        {
            using (positionsLocker.Using())
            {
                return StrategyPositionsToStringInternal();
            }
        }

        protected string SymbolPositionsToStringInternal()
        {
            var sb = new StringBuilder();
            foreach (var kvp in positions)
            {
                sb.AppendLine();
                var symbol = Factory.Symbol.LookupSymbol(kvp.Key);
                var position = kvp.Value;
                sb.Append(symbol + " " + position);
            }
            return sb.ToString();
        }

        protected string StrategyPositionsToStringInternal()
        {
            var sb = new StringBuilder();
            foreach (var kvp in strategyPositions)
            {
                var strategyPosition = kvp.Value;
                sb.AppendLine(strategyPosition.ToString());
            }
            return sb.ToString();
        }
    }
}