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
        protected Dictionary<long, ActiveList<PhysicalOrder>> ordersBySymbol = new Dictionary<long, ActiveList<PhysicalOrder>>();
        protected Dictionary<long, PhysicalOrder> ordersByBrokerId = new Dictionary<long, PhysicalOrder>();
        protected Dictionary<long, PhysicalOrder> filledOrdersByBrokerId = new Dictionary<long, PhysicalOrder>();
        protected Dictionary<long, PhysicalOrder> ordersBySerial = new Dictionary<long, PhysicalOrder>();
        protected Dictionary<long, SymbolPosition> positions = new Dictionary<long, SymbolPosition>();
        protected Dictionary<int, StrategyPosition> strategyPositions = new Dictionary<int, StrategyPosition>();
        private Pool<PhysicalOrderDefault> orderPool = Factory.Parallel.Pool<PhysicalOrderDefault>();
        private string name;
        private int callerId;
        private Comparison<PhysicalOrder> physicalComparison;


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
            log.Register(this);
            callerId = orderPool.GetCallerId("PhysicalOrderCache");
            physicalComparison = OnComparison;
        }

        public bool TryGetOrders( SymbolInfo symbol, out ActiveList<PhysicalOrder> orders)
        {
            if( ! ordersBySymbol.TryGetValue(symbol.BinaryIdentifier, out orders))
            {
                orders = ordersBySymbol[symbol.BinaryIdentifier] = new ActiveList<PhysicalOrder>();
            }
            return true;
        }

        public virtual void AssertAtomic() { }
        private int sortTimesCount = 64;
        private PhysicalOrder[] sortTimesArray;
        private int filledCounter;
        public void TryClearFilledOrders()
        {
            ++filledCounter;
            if (filledCounter < (sortTimesCount >> 1) * ordersBySymbol.Count)
            {
                return;
            }
            filledCounter = 0;
            if (filledOrdersByBrokerId.Count < (sortTimesCount >> 1) * ordersBySymbol.Count)
            {
                return;
            }
            if (debug) log.DebugFormat("TryClearFilledOrders() {0} orders", filledOrdersByBrokerId.Count);
            AssertAtomic();

            if( sortTimesArray == null || sortTimesArray.Length < sortTimesCount * ordersBySymbol.Count)
            {
                sortTimesArray = new PhysicalOrder[sortTimesCount * ordersBySymbol.Count];
            }
            else
            {
                Array.Clear(sortTimesArray, 0, sortTimesArray.Length);
            }
            var filledCount = 0;

            foreach (var kvp in filledOrdersByBrokerId)
            {
                var order = kvp.Value;
                if (filledCount >= sortTimesArray.Length)
                {
                    break;
                }
                sortTimesArray[filledCount] = order;
                ++filledCount;
            }

            if (debug) log.DebugFormat("Found {0} filled orders.", filledCount);
            Array.Sort(sortTimesArray, physicalComparison);
            if (trace) log.TraceFormat("Sorted orders by last modify time:");
            var count = 0;
            for (var x = 0; x < sortTimesArray.Length; x++)
            {
                var order = sortTimesArray[x];
                if (order != null)
                {
                    if (count >= filledCount >> 1)
                    {
                        if (debug) log.DebugFormat("Removing order: {0}", sortTimesArray[x]);
                        var removed = RemoveOrderInternal(order.BrokerOrder);
                        if( removed != null)
                        {
                            orderPool.Free((PhysicalOrderDefault)removed);
                        }
                    }
                    else
                    {
                        if (debug) log.DebugFormat("Keeping order: {0}", sortTimesArray[x]);
                    }
                    count++;
                }
            }
        }

        public void MoveToFilled(PhysicalOrder order)
        {
            RemoveOrderInternal(order.BrokerOrder);
            filledOrdersByBrokerId[order.BrokerOrder] = order;
            if (debug) log.DebugFormat("Moved to filled orders list {0}", order);
            TryClearFilledOrders();
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
                    var newStrategyPosition = new StrategyPositionDefault();
                    newStrategyPosition.Initialize(position.Id, position.Symbol);
                    this.strategyPositions.Add(position.Id, newStrategyPosition);
                    strategyPosition = newStrategyPosition;
                }
                strategyPosition.TrySetPosition(position.ExpectedPosition);
            }
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
                    var newStrategyPosition = new StrategyPositionDefault();
                    newStrategyPosition.Initialize(strategyId, symbol);
                    this.strategyPositions.Add(strategyId, newStrategyPosition);
                    strategyPosition = newStrategyPosition;

                }
                strategyPosition.SetExpectedPosition(position);
            }
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
            if( ordersByBrokerId.TryGetValue(brokerOrder, out order))
            {
                return true;
            }
            if (filledOrdersByBrokerId.TryGetValue(brokerOrder, out order))
            {
                return true;
            }
            return false;
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
            if (!TryGetOrderById(brokerOrder, out order))
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
            var removed = RemoveOrderInternal(order.OriginalOrder.BrokerOrder);
            if( removed != null)
            {
                orderPool.Free((PhysicalOrderDefault)removed);
            }
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
            var removed = RemoveOrderInternal(clientOrderId);
            if( removed != null)
            {
                orderPool.Free((PhysicalOrderDefault)removed);
            }
            if (removed != null && removed.OriginalOrder != null)
            {
                removed.OriginalOrder.ReplacedBy = null;
            }
        }

        private PhysicalOrder RemoveOrderInternal(long clientOrderId)
        {
            if (clientOrderId == 0)
            {
                return null;
            }
            PhysicalOrder order = null;
            filledOrdersByBrokerId.Remove(clientOrderId);
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
                ActiveList<PhysicalOrder> orders;
                if( ordersBySymbol.TryGetValue(order.Symbol.BinaryIdentifier, out orders))
                {
                    for( var current = orders.First; current != null; current = current.Next)
                    {
                        if( current.Value.BrokerOrder == order.BrokerOrder)
                        {
                            orders.Remove(current);
                            break;
                        }
                    }
                }
                ordersBySequence.Remove(order.Sequence);
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

        public bool HasCreateOrder(PhysicalOrder order)
        {
            ActiveList<PhysicalOrder> tempCreateOrders;
            if( !TryGetOrders(order.Symbol, out tempCreateOrders))
            {
                return false;
            }
            for (var current = tempCreateOrders.First; current != null; current = current.Next )
            {
                var queueOrder = current.Value;
                if (queueOrder.OrderState == OrderState.Filled)
                {
                    throw new ApplicationException("Filled order in active order list: " + order);
                }
                if (queueOrder.Action == OrderAction.Create && order.LogicalSerialNumber == queueOrder.LogicalSerialNumber)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG542, queueOrder);
                    return true;
                }
            }
            return false;
        }

        public bool HasCancelOrder(PhysicalOrder order)
        {
            ActiveList<PhysicalOrder> tempCreateOrders;
            if (!TryGetOrders(order.Symbol, out tempCreateOrders))
            {
                return false;
            }
            for (var current = tempCreateOrders.First; current != null; current = current.Next)
            {
                var queueOrder = current.Value;
                if (queueOrder.OrderState == OrderState.Filled)
                {
                    throw new ApplicationException("Filled order in active order list: " + order);
                }
                if (queueOrder.OriginalOrder != null && order.OriginalOrder.BrokerOrder == queueOrder.OriginalOrder.BrokerOrder)
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
            SetOrderBySymbol(order);
#if DEBUG
            var count = 0;
            foreach( var kvp in ordersBySymbol)
            {
                count += kvp.Value.Count;
            }
            if( ordersByBrokerId.Count != count)
            {
                throw new ApplicationException("Mismatch order count between 'by broker id' and 'by symbol'");
            }
#endif
        }

        protected void SetOrderBySymbol(PhysicalOrder order)
        {
            ActiveList<PhysicalOrder> orders;
            if( !ordersBySymbol.TryGetValue(order.Symbol.BinaryIdentifier, out orders))
            {
                orders = ordersBySymbol[order.Symbol.BinaryIdentifier] = new ActiveList<PhysicalOrder>();
            }
            var match = false;
            for( var current = orders.First; current != null; current = current.Next)
            {
                if( current.Value.BrokerOrder == order.BrokerOrder)
                {
                    current.Value = order;
                    match = true;
                    break;
                }
            }
            if( !match)
            {
                orders.AddLast(order);
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