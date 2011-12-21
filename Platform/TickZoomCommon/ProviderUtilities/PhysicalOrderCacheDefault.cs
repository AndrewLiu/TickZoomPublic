﻿using System;
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
            if (log != null)
            {
                info = log.IsDebugEnabled;
                debug = log.IsDebugEnabled;
                trace = log.IsTraceEnabled;
            }
        }

        protected class SymbolPosition
        {
            public long Position;
            public override string ToString()
            {
                return Position.ToString();
            }
        }

        protected Dictionary<int, CreateOrChangeOrder> ordersBySequence = new Dictionary<int, CreateOrChangeOrder>();
        protected Dictionary<string, CreateOrChangeOrder> ordersByBrokerId = new Dictionary<string, CreateOrChangeOrder>();
        protected Dictionary<long, CreateOrChangeOrder> ordersBySerial = new Dictionary<long, CreateOrChangeOrder>();
        protected Dictionary<long, SymbolPosition> positions = new Dictionary<long, SymbolPosition>();
        protected Dictionary<int, StrategyPosition> strategyPositions = new Dictionary<int, StrategyPosition>();
        private TaskLock ordersLocker = new TaskLock();
        protected SimpleLock cacheLocker = new SimpleLock();
        private PhysicalOrderLock physicalOrderLock;

        public PhysicalOrderCacheDefault()
        {
            log = Factory.SysLog.GetLogger(typeof(PhysicalOrderCacheDefault));
            log.Register(this);
            physicalOrderLock = new PhysicalOrderLock(this);
        }

        public class PhysicalOrderLock : IDisposable
        {
            private PhysicalOrderCache lockedCache;
            internal PhysicalOrderLock(PhysicalOrderCache cache)
            {
                lockedCache = cache;
            }
            public void Dispose()
            {
                lockedCache.EndTransaction();
            }
        }

        public IDisposable BeginTransaction()
        {
            cacheLocker.Lock();
            return physicalOrderLock;
        }

        public void EndTransaction()
        {
            cacheLocker.Unlock();
        }

        public bool IsLocked
        {
            get { return cacheLocker.IsLocked; }
        }

        public void AssertAtomic()
        {
            if (!IsLocked)
            {
                var message = "Attempt to modify PhysicalOrder w/o locking PhysicalOrderStore first.";
                log.Error(message + "\n" + Environment.StackTrace);
                //throw new ApplicationException(message);
            }
        }

        public Iterable<CreateOrChangeOrder> GetActiveOrders(SymbolInfo symbol)
        {
            AssertAtomic();
            var result = new ActiveList<CreateOrChangeOrder>();
            var list = GetOrders((o) => o.Symbol == symbol);
            foreach (var order in list)
            {
                if (order.OrderState != OrderState.Filled)
                {
                    result.AddLast(order);
                }
            }
            return result;
        }

        public void SyncPositions(Iterable<StrategyPosition> strategyPositions)
        {
            if (trace)
            {
                for (var node = strategyPositions.First; node != null; node = node.Next)
                {
                    var sp = node.Value;
                    log.Trace("Received strategy position. " + sp);
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
        }

        public void SetActualPosition(SymbolInfo symbol, long position)
        {
            using (positionsLocker.Using())
            {
                if (debug) log.Debug("SetActualPosition( " + symbol + " = " + position + ")");
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

        public bool TryGetOrderById(string brokerOrder, out CreateOrChangeOrder order)
        {
            AssertAtomic();
            if (brokerOrder == null)
            {
                order = null;
                return false;
            }
            using (ordersLocker.Using())
            {
                return ordersByBrokerId.TryGetValue((string)brokerOrder, out order);
            }
        }

        public bool TryGetOrderBySequence(int sequence, out CreateOrChangeOrder order)
        {
            AssertAtomic();
            if (sequence == 0)
            {
                order = null;
                return false;
            }
            using (ordersLocker.Using())
            {
                return ordersBySequence.TryGetValue(sequence, out order);
            }
        }

        public CreateOrChangeOrder GetOrderById(string brokerOrder)
        {
            AssertAtomic();
            using (ordersLocker.Using())
            {
                CreateOrChangeOrder order;
                if (!ordersByBrokerId.TryGetValue((string)brokerOrder, out order))
                {
                    throw new ApplicationException("Unable to find order for id: " + brokerOrder);
                }
                return order;
            }
        }

        public CreateOrChangeOrder RemoveOrder(string clientOrderId)
        {
            if (trace) log.Trace("RemoveOrder( " + clientOrderId + ")");
            AssertAtomic();
            if (string.IsNullOrEmpty(clientOrderId))
            {
                return null;
            }
            using (ordersLocker.Using())
            {
                CreateOrChangeOrder order = null;
                if (ordersByBrokerId.TryGetValue(clientOrderId, out order))
                {
                    var result = ordersByBrokerId.Remove(clientOrderId);
                    if (result && trace) log.Trace("Removed order by broker id " + clientOrderId + ": " + order);
                    CreateOrChangeOrder orderBySerial;
                    if (ordersBySerial.TryGetValue(order.LogicalSerialNumber, out orderBySerial))
                    {
                        if (orderBySerial.BrokerOrder.Equals(clientOrderId))
                        {
                            var result2 = ordersBySerial.Remove(order.LogicalSerialNumber);
                            if (result2 && trace) log.Trace("Removed order by logical id " + order.LogicalSerialNumber + ": " + orderBySerial);
                        }
                    }
                    return order;
                }
                else
                {
                    return null;
                }
            }
        }

        public bool TryGetOrderBySerial(long logicalSerialNumber, out CreateOrChangeOrder order)
        {
            AssertAtomic();
            using (ordersLocker.Using())
            {
                return ordersBySerial.TryGetValue(logicalSerialNumber, out order);
            }
        }

        public CreateOrChangeOrder GetOrderBySerial(long logicalSerialNumber)
        {
            AssertAtomic();
            using (ordersLocker.Using())
            {
                CreateOrChangeOrder order;
                if (!ordersBySerial.TryGetValue(logicalSerialNumber, out order))
                {
                    throw new ApplicationException("Unable to find order by serial for id: " + logicalSerialNumber);
                }
                return order;
            }
        }

        public bool HasCreateOrder(CreateOrChangeOrder order)
        {
            var createOrderQueue = GetActiveOrders(order.Symbol);
            for (var current = createOrderQueue.First; current != null; current = current.Next)
            {
                var queueOrder = current.Value;
                if (order.Action == OrderAction.Create && order.LogicalSerialNumber == queueOrder.LogicalSerialNumber)
                {
                    if (debug) log.Debug("Create ignored because order was already on create order queue: " + queueOrder);
                    return true;
                }
            }
            return false;
        }

        public bool HasCancelOrder(PhysicalOrder order)
        {
            var cancelOrderQueue = GetActiveOrders(order.Symbol);
            for (var current = cancelOrderQueue.First; current != null; current = current.Next)
            {
                var clientId = current.Value;
                if (clientId.OriginalOrder != null && order.OriginalOrder.BrokerOrder == clientId.OriginalOrder.BrokerOrder)
                {
                    if (debug) log.Debug("Cancel or Changed ignored because previous order order working for: " + order);
                    return true;
                }
            }
            return false;
        }

        public void SetOrder(CreateOrChangeOrder order)
        {
            AssertAtomic();
            using (ordersLocker.Using())
            {
                if (trace) log.Trace("Assigning order " + order.BrokerOrder + " with " + order.LogicalSerialNumber);
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
        }

        public List<CreateOrChangeOrder> GetOrders(Func<CreateOrChangeOrder, bool> select)
        {
            AssertAtomic();
            var list = new List<CreateOrChangeOrder>();
            using (ordersLocker.Using())
            {
                foreach (var kvp in ordersByBrokerId)
                {
                    var order = kvp.Value;
                    if (select(order))
                    {
                        list.Add(order);
                    }
                }
            }
            return list;
        }

        public void ResetLastChange()
        {
            AssertAtomic();
            if (debug) log.Debug("Resetting last change time for all physical orders.");
            using (ordersLocker.Using())
            {
                foreach (var kvp in ordersByBrokerId)
                {
                    var order = kvp.Value;
                    order.ResetLastChange();
                }
            }
        }

        public string OrdersToString()
        {
            using (ordersLocker.Using())
            {
                var sb = new StringBuilder();
                foreach (var kvp in ordersByBrokerId)
                {
                    sb.AppendLine(kvp.Value.ToString());
                }
                return sb.ToString();
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
                    if( cacheLocker.IsLocked)
                    {
                        return;
                    }
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
                var symbol = Factory.Symbol.LookupSymbol(kvp.Key);
                var position = kvp.Value;
                sb.AppendLine(symbol + " " + position);
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