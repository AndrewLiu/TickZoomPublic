#region Copyright
/*
 * Software: TickZoom Trading Platform
 * Copyright 2009 M. Wayne Walter
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 * 
 * Business use restricted to 30 days except as otherwise stated in
 * in your Service Level Agreement (SLA).
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <http://www.tickzoom.org/wiki/Licenses>
 * or write to Free Software Foundation, Inc., 51 Franklin Street,
 * Fifth Floor, Boston, MA  02110-1301, USA.
 * 
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Interceptors
{
    public class FillSimulatorPhysical : FillSimulator, LogAware
    {
        private static readonly Log staticLog = Factory.SysLog.GetLogger(typeof(FillSimulatorPhysical));
        private Log log;
        private volatile bool trace = staticLog.IsTraceEnabled;
        private volatile bool verbose = staticLog.IsVerboseEnabled;
        private volatile bool debug = staticLog.IsDebugEnabled;
        private FillSimulatorLogic fillLogic;
        private bool isChanged;
        private bool enableSyncTicks;
        public void RefreshLogLevel()
        {
            if (log != null)
            {
                debug = log.IsDebugEnabled;
                trace = log.IsTraceEnabled;
            }
        }
        private struct FillWrapper
        {
            public bool IsCounterSet;
            public PhysicalFill Fill;
            public PhysicalOrder Order;
        }
        private Queue<FillWrapper> fillQueue = new Queue<FillWrapper>();
        private struct RejectWrapper
        {
            public PhysicalOrder Order;
            public bool RemoveOriginal;
            public string Message;
        }
        private Queue<RejectWrapper> rejectQueue = new Queue<RejectWrapper>();

        private PartialFillSimulation partialFillSimulation;

        private Dictionary<long, PhysicalOrder> orderMap = new Dictionary<long, PhysicalOrder>();
        private ActiveList<PhysicalOrder> increaseOrders = new ActiveList<PhysicalOrder>();
        private ActiveList<PhysicalOrder> decreaseOrders = new ActiveList<PhysicalOrder>();
        private ActiveList<PhysicalOrder> marketOrders = new ActiveList<PhysicalOrder>();
        private ActiveList<PhysicalOrder> touchOrders = new ActiveList<PhysicalOrder>();
        private NodePool<PhysicalOrder> nodePool = new NodePool<PhysicalOrder>();
        private object orderMapLocker = new object();
        private bool isOpenTick = false;
        private TimeStamp openTime;

        private Action<PhysicalFill,PhysicalOrder> onPhysicalFill;
        private Action<PhysicalOrder, string> onRejectOrder;
        private Action<long> onPositionChange;
        private bool useSyntheticMarkets = true;
        private bool useSyntheticStops = true;
        private bool useSyntheticLimits = true;
        private SymbolInfo symbol;
        private int actualPosition = 0;
        private TickSync tickSync;
        private TickIO currentTick = Factory.TickUtil.TickIO();
        private PhysicalOrderConfirm confirmOrders;
        private bool isBarData = false;
        private bool createExitStrategyFills = false;
        // Randomly rotate the partial fills but using a fixed
        // seed so that test results are reproducable.
        private Random random = new Random(1234);
        private long minimumTick;
        private static int maxPartialFillsPerOrder = 1;
        private volatile bool isOnline = false;
        private string name;
        private bool createActualFills;
        private TriggerController triggers;
        private Dictionary<long, long> serialTriggerMap = new Dictionary<long, long>();

        public FillSimulatorPhysical(string name, SymbolInfo symbol, bool createExitStrategyFills, bool createActualFills, TriggerController triggers)
        {
            this.symbol = symbol;
            this.name = name;
            this.triggers = triggers;
            this.minimumTick = symbol.MinimumTick.ToLong();
            this.tickSync = SyncTicks.GetTickSync(symbol.BinaryIdentifier);
            this.createExitStrategyFills = createExitStrategyFills;
            this.log = Factory.SysLog.GetLogger(typeof(FillSimulatorPhysical).FullName + "." + symbol.ExpandedSymbol.StripInvalidPathChars() + "." + name);
            this.log.Register(this);
            this.createActualFills = createActualFills;
            fillLogic = new FillSimulatorLogic(name, symbol, FillCallback);
            IsChanged = true;
            PartialFillSimulation = symbol.PartialFillSimulation;
        }

        private bool hasCurrentTick = false;
        public void OnOpen(Tick tick)
        {
            if (trace) log.TraceFormat("OnOpen({0})", tick);
            isOpenTick = true;
            openTime = tick.Time;
            if (!tick.IsQuote && !tick.IsTrade)
            {
                throw new ApplicationException("tick w/o either trade or quote data? " + tick);
            }
            currentTick.Inject(tick.Extract());
            hasCurrentTick = true;
            IsChanged = true;
        }

        public Iterable<PhysicalOrder> GetActiveOrders(SymbolInfo symbol)
        {
            ActiveList<PhysicalOrder> activeOrders = new ActiveList<PhysicalOrder>();
            activeOrders.AddLast(increaseOrders);
            activeOrders.AddLast(decreaseOrders);
            activeOrders.AddLast(marketOrders);
            activeOrders.AddLast(touchOrders);
            return activeOrders;
        }

        public bool OnChangeBrokerOrder(PhysicalOrder other)
        {
            var order = other.Clone();
            if (debug) log.DebugFormat("OnChangeBrokerOrder( {0})", order);
            if( order.OriginalOrder != null)
            {
                var origOrder = CancelBrokerOrder(order.OriginalOrder.BrokerOrder);
                if (origOrder == null)
                {
                    if (debug) log.DebugFormat("PhysicalOrder too late to change. Already filled or canceled, ignoring.");
                    var message = "No such order";
                    if (onRejectOrder != null)
                    {
                        SendReject(order, true, message);
                    }
                    else
                    {
                        throw new ApplicationException(message + " while handling order: " + order);
                    }
                    return true;
                }
            }
            order.OriginalOrder = null; // Original order was canceled.
            if (CreateBrokerOrder(order))
            {
                if (confirmOrders != null) confirmOrders.ConfirmChange(order.BrokerOrder, true);
                UpdateCounts();
            }
            return true;
        }

        public bool TryGetOrderById(long orderId, out PhysicalOrder physicalOrder)
        {
            LogOpenOrders();
            lock (orderMapLocker)
            {
                return orderMap.TryGetValue(orderId, out physicalOrder);
            }
        }


        public PhysicalOrder GetOrderById(long orderId)
        {
            PhysicalOrder order;
            lock (orderMapLocker)
            {
                if (!TryGetOrderById(orderId, out order))
                {
                    throw new ApplicationException(symbol + ": Cannot find physical order by id: " + orderId);
                }
            }
            return order;
        }


        private bool CreateBrokerOrder(PhysicalOrder order)
        {
#if VERIFYSIDE
            if (!VerifySide(order))
            {
                return false;
            }
#endif
            lock (orderMapLocker)
            {
                try
                {
                    orderMap.Add(order.BrokerOrder, order);
                    if (trace) log.TraceFormat("Added order {0}", order.BrokerOrder);
                }
                catch (ArgumentException)
                {
                    throw new ApplicationException("A broker order id of " + order.BrokerOrder + " was already added.");
                }
            }
            TriggerOperation operation = default(TriggerOperation);
            var buyStop = order.Side == OrderSide.Buy && order.Type == OrderType.Stop;
            var sellLimit = order.Side != OrderSide.Buy && order.Type == OrderType.Limit;

            switch (order.Type)
            {
                case OrderType.Market:
                    break;
                case OrderType.Stop:
                case OrderType.Limit:
                    if( buyStop || sellLimit)
                    {
                        operation = TriggerOperation.GreaterOrEqual;
                    }
                    else
                    {
                        operation = TriggerOperation.LessOrEqual;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected OrderType: " + order.Type);
            }
            if (triggers != null)
            {
                var triggerId = triggers.AddTrigger(order.LogicalSerialNumber, TriggerData.Price, operation, order.Price, TriggerCallback);
                serialTriggerMap[order.LogicalSerialNumber] = triggerId;
            }
            SortAdjust(order);
            IsChanged = true;
            OrderChanged();
            LogOpenOrders();
            return true;
        }

        private void TriggerCallback(long logicalSerialNumber)
        {
            IsChanged = false;
            ClearOrderChanged();
            if (hasCurrentTick)
            {
                ProcessOrdersInternal(currentTick);
            }
            else
            {
                if (debug) log.DebugFormat("Skipping TriggerCallback because HasCurrentTick is {0}", hasCurrentTick);
            }
        }

        private PhysicalOrder CancelBrokerOrder(long oldOrderId)
        {
            PhysicalOrder physicalOrder;
            if (TryGetOrderById(oldOrderId, out physicalOrder))
            {
                var node = (ActiveListNode<PhysicalOrder>)physicalOrder.Reference;
                if (node.List != null)
                {
                    node.List.Remove(node);
                }
                nodePool.Free(node);
                lock (orderMapLocker)
                {
                    if( debug) log.DebugFormat("Canceling by id {0}. Order: {1}", oldOrderId, physicalOrder);
                    orderMap.Remove(oldOrderId);
                }
                if (triggers != null)
                {
                    var triggerId = serialTriggerMap[physicalOrder.LogicalSerialNumber];
                    serialTriggerMap.Remove(physicalOrder.LogicalSerialNumber);
                    triggers.RemoveTrigger(triggerId);
                }
                LogOpenOrders();
            }
            return physicalOrder;
        }

        public bool OnCreateBrokerOrder(PhysicalOrder other)
        {
            var order = other.Clone();
            if (debug) log.DebugFormat("OnCreateBrokerOrder( {0})", order);
            if (order.RemainingSize <= 0)
            {
                throw new ApplicationException("Sorry, Size of order must be greater than zero: " + order);
            }
            if( CreateBrokerOrder(order))
            {
                if (confirmOrders != null) confirmOrders.ConfirmCreate(order.BrokerOrder, true);
                UpdateCounts();
            }
            return true;
        }

        public bool OnCancelBrokerOrder(PhysicalOrder order)
        {
            if (debug) log.DebugFormat("OnCancelBrokerOrder( {0})", order.OriginalOrder.BrokerOrder);
            var origOrder = CancelBrokerOrder(order.OriginalOrder.BrokerOrder);
            if (origOrder == null)
            {
                if (debug) log.DebugFormat("PhysicalOrder too late to change. Already filled or canceled, ignoring.");
                var message = "No such order";
                if (onRejectOrder != null)
                {
                    SendReject(order, true, message);
                }
                else
                {
                    throw new ApplicationException(message + " while handling order: " + order);
                }
                return true;
            }
            origOrder.ReplacedBy = order;
            if (confirmOrders != null) confirmOrders.ConfirmCancel(order.OriginalOrder.BrokerOrder, true);
            UpdateCounts();
            return true;
        }

        public int ProcessOrders()
        {
            IsChanged = false;
            ClearOrderChanged();
            if (hasCurrentTick)
            {
                ProcessOrdersInternal(currentTick);
            }
            else
            {
                if (debug) log.DebugFormat("Skipping ProcessOrders because HasCurrentTick is {0}", hasCurrentTick);
            }
            return 1;
        }

        public int ProcessAdjustments()
        {
            if (hasCurrentTick)
            {
                ProcessAdjustmentsInternal(currentTick);
            }
            return 1;
        }

        public void StartTick(Tick lastTick)
        {
            if (trace) log.TraceFormat("StartTick({0})", lastTick);
            if (!lastTick.IsQuote && !lastTick.IsTrade)
            {
                throw new ApplicationException("tick w/o either trade or quote data? " + lastTick);
            }
            currentTick.Inject(lastTick.Extract());
            hasCurrentTick = true;
            IsChanged = true;
        }

        public void LogActiveOrders()
        {
            var orders = GetActiveOrders(symbol);
            for (var current = orders.First; current != null; current = current.Next)
            {
                var order = current.Value;
                if (debug) log.DebugFormat(order.ToString());
            }
        }
        private void ProcessAdjustmentsInternal(Tick tick)
        {
            if (verbose) log.VerboseFormat("ProcessAdjustments( {0}, {1} )", symbol, tick);
            if (symbol == null)
            {
                throw new ApplicationException("Please set the Symbol property for the " + GetType().Name + ".");
            }
            for (var node = marketOrders.First; node != null; node = node.Next)
            {
                var order = node.Value;
                if (order.LogicalOrderId == 0)
                {
                    OnProcessOrder(order, tick);
                }
            }
            if (onPhysicalFill == null)
            {
                throw new ApplicationException("Please set the OnPhysicalFill property.");
            }
            else
            {
                FlushFillQueue();
            }
        }

        private void OnProcessOrder(PhysicalOrder order, Tick tick)
        {
            if (tick.UtcTime < order.UtcCreateTime)
            {
                //if (trace) log.Trace
                log.Info("Skipping check of " + order.Type + " on tick UTC time " + tick.UtcTime + "." + order.UtcCreateTime.Microsecond + " because earlier than order create UTC time " + order.UtcCreateTime + "." + order.UtcCreateTime.Microsecond);
                return;
            }
            if( tick.UtcTime > order.LastReadTime )
            {
                order.LastReadTime = tick.UtcTime;
                fillLogic.TryFillOrder(order, tick);
            }
        }

        private void ProcessOrdersInternal(Tick tick)
        {
            if (isOpenTick && tick.Time > openTime)
            {
                if (trace)
                {
                    log.TraceFormat("ProcessOrders( {0}, {1} ) [OpenTick]", symbol, tick);
                }
                isOpenTick = false;
            }
            else if (trace)
            {
                log.TraceFormat("ProcessOrders( {0}, {1} )", symbol, tick);
            }
            if (symbol == null)
            {
                throw new ApplicationException("Please set the Symbol property for the " + GetType().Name + ".");
            }
            if (trace) log.TraceFormat("Orders: Touch {0}, Market {1}, Increase {2}, Decrease {3}", touchOrders.Count, marketOrders.Count, increaseOrders.Count, decreaseOrders.Count);
            if (touchOrderCount > 0)
            {
                for (var node = touchOrders.First; node != null; node = node.Next)
                {
                    var order = node.Value;
                    OnProcessOrder(order, tick);
                }
            }
            if (marketOrderCount > 0)
            {
                for (var node = marketOrders.First; node != null; node = node.Next)
                {
                    var order = node.Value;
                    OnProcessOrder(order, tick);
                }
            }
            if (increaseOrderCount > 0)
            {
                for (var node = increaseOrders.First; node != null; node = node.Next)
                {
                    var order = node.Value;
                    OnProcessOrder(order, tick);
                }
            }
            if (decreaseOrderCount > 0)
            {
                for (var node = decreaseOrders.First; node != null; node = node.Next)
                {
                    var order = node.Value;
                    OnProcessOrder(order, tick);
                }
            }
            if (onPhysicalFill == null)
            {
                throw new ApplicationException("Please set the OnPhysicalFill property.");
            }
            else
            {
                FlushFillQueue();
            }
        }

        public void FlushFillQueue()
        {
            if (!isOnline)
            {
                if (verbose) log.VerboseFormat("Unable to flush fill queue yet because isOnline is {0}", isOnline);
                return;
            }
            while (fillQueue.Count > 0)
            {
                var wrapper = fillQueue.Dequeue();
                if (debug) log.DebugFormat("Dequeuing fill ( isOnline {0}): {1}", isOnline, wrapper.Fill);
                if (enableSyncTicks && !wrapper.IsCounterSet) tickSync.AddPhysicalFill(wrapper.Fill);
                onPhysicalFill(wrapper.Fill, wrapper.Order);
            }
            while (rejectQueue.Count > 0)
            {
                var wrapper = rejectQueue.Dequeue();
                if (debug) log.DebugFormat("Dequeuing reject {0}", wrapper.Order);
                onRejectOrder(wrapper.Order, wrapper.Message);
            }
        }

        private void LogOpenOrders()
        {
            if (debug)
            {
                log.DebugFormat("Found {0} open orders for {1}:", orderMap.Count, symbol);
                lock (orderMapLocker)
                {
                    foreach (var kvp in orderMap)
                    {
                        var order = kvp.Value;
                        log.DebugFormat("{0}",order);
                    }
                    LogOrderList(touchOrders, "Touch orders");
                    LogOrderList(marketOrders, "Market orders");
                    LogOrderList(increaseOrders, "Increase orders");
                    LogOrderList(decreaseOrders, "Decrease orders");
                }
            }
        }

        private void LogOrderList(ActiveList<PhysicalOrder> list, string name)
        {
            if( list.Count > 0)
            {
                log.DebugFormat( "   {0} {1}", name, list.Count);
            }
            for( var current = list.First; current != null; current = current.Next)
            {
                log.DebugFormat("    {0}", current.Value);
            }
        }

        private int decreaseOrderCount;
        private int increaseOrderCount;
        private int marketOrderCount;
        private int touchOrderCount;

        private void UpdateCounts()
        {
            decreaseOrderCount = decreaseOrders.Count;
            increaseOrderCount = increaseOrders.Count;
            marketOrderCount = marketOrders.Count;
            touchOrderCount = touchOrders.Count;
        }

        public int OrderCount
        {
            get { return decreaseOrderCount + increaseOrderCount + marketOrderCount; }
        }

        private void SortAdjust(PhysicalOrder order)
        {
            var buyStop = order.Side == OrderSide.Buy && order.Type == OrderType.Stop;
            var sellLimit = order.Side != OrderSide.Buy && order.Type == OrderType.Limit;
            switch (order.Type)
            {
                case OrderType.Limit:
                case OrderType.Stop:
                    if( buyStop || sellLimit)
                    {
                        SortAdjust(increaseOrders, order, (x, y) => x.Price - y.Price);
                    }
                    else
                    {
                        SortAdjust(decreaseOrders, order, (x, y) => y.Price - x.Price);
                    }
                    break;
                case OrderType.Market:
                    if( order.IsTouch)
                    {
                        Adjust(touchOrders, order);
                    }
                    else
                    {
                        Adjust(marketOrders, order);
                    }
                    break;
                default:
                    throw new ApplicationException("Unexpected order type: " + order.Type);
            }
        }

        private void AssureNode(PhysicalOrder order)
        {
            if (order.Reference == null)
            {
                order.Reference = nodePool.Create(order);
            }
        }

        private void Adjust(ActiveList<PhysicalOrder> list, PhysicalOrder order)
        {
            AssureNode(order);
            var node = (ActiveListNode<PhysicalOrder>)order.Reference;
            if (node.List == null)
            {
                list.AddLast(node);
            }
            else if (!node.List.Equals(list))
            {
                node.List.Remove(node);
                list.AddLast(node);
            }
        }

        private void SortAdjust(ActiveList<PhysicalOrder> list, PhysicalOrder order, Func<PhysicalOrder, PhysicalOrder, double> compare)
        {
            AssureNode(order);
            var orderNode = (ActiveListNode<PhysicalOrder>)order.Reference;
            if (orderNode.List == null || !orderNode.List.Equals(list))
            {
                if (orderNode.List != null)
                {
                    orderNode.List.Remove(orderNode);
                }
                bool found = false;
                var next = list.First;
                for (var node = next; node != null; node = next)
                {
                    next = node.Next;
                    var other = node.Value;
                    if (object.ReferenceEquals(order, other))
                    {
                        found = true;
                        break;
                    }
                    else
                    {
                        var result = compare(order, other);
                        if (result < 0)
                        {
                            list.AddBefore(node, orderNode);
                            found = true;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    list.AddLast(orderNode);
                }
            }
        }

        private void FillCallback(Order order, double price, Tick tick)
        {
            var physicalOrder = (PhysicalOrder)order;
            int size = 0;
            switch (order.Side)
            {
                case OrderSide.Buy:
                    size = physicalOrder.RemainingSize;
                    break;
                default:
                    size = -physicalOrder.RemainingSize;
                    break;
            }
            CreatePhysicalFillHelper(size, price, tick.Time, tick.UtcTime, physicalOrder);
        }

        private void OrderSideWrongReject(PhysicalOrder order)
        {
            var message = "Sorry, improper setting of a " + order.Side + " order when position is " + actualPosition;
            lock (orderMapLocker)
            {
                orderMap.Remove(order.BrokerOrder);
            }
            if (onRejectOrder != null)
            {
                if (debug) log.DebugFormat("Rejecting order because position is {0} but order side was {1}: {2}", actualPosition, order.Side, order);
                SendReject(order, true, message);
            }
            else
            {
                throw new ApplicationException(message + " while handling order: " + order);
            }
        }

        private void SendReject(PhysicalOrder order, bool removeOriginal, string  message)
        {
            var wrapper = new RejectWrapper
                              {
                                  Order = order,
                                  RemoveOriginal = removeOriginal,
                                  Message = message
                              };
            rejectQueue.Enqueue(wrapper);
        }

        private bool VerifySellSide(PhysicalOrder order)
        {
            var result = true;
            if (actualPosition > 0)
            {
                if (order.Side != OrderSide.Sell)
                {
                    OrderSideWrongReject(order);
                    result = false;
                }
            }
            else
            {
                if (order.Side != OrderSide.SellShort)
                {
                    OrderSideWrongReject(order);
                    result = false;
                }
            }
            return result;
        }

        private bool VerifyBuySide(PhysicalOrder order)
        {
            var result = true;
            if (order.Side != OrderSide.Buy)
            {
                OrderSideWrongReject(order);
                result = false;
            }
            return result;
        }

        private void CreatePhysicalFillHelper(int totalSize, double price, TimeStamp time, TimeStamp utcTime, PhysicalOrder order)
        {
            if (debug) log.DebugFormat("Filling order: {0}", order);
            var remainingSize = totalSize;
            var split = 1;
            var numberFills = split;
            switch (partialFillSimulation)
            {
                case PartialFillSimulation.None:
                    break;
                case PartialFillSimulation.PartialFillsTillComplete:
                    numberFills = split = random.Next(maxPartialFillsPerOrder) + 1;
                    break;
                case PartialFillSimulation.PartialFillsIncomplete:
                    if (order.Type == OrderType.Limit)
                    {
                        split = 5;
                        numberFills = 3;
                        if (debug) log.DebugFormat("True Partial of only {0} fills out of {1} for {2}", numberFills, split, order);
                    }
                    else
                    {
                        numberFills = split = random.Next(maxPartialFillsPerOrder) + 1;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unrecognized partial fill simulation: " + PartialFillSimulation);
            }
            var lastSize = totalSize / split;
            var cumulativeQuantity = 0;
            if (lastSize == 0) lastSize = totalSize;
            var count = 0;
            if( debug) log.DebugFormat("{0} totalSize {1}, split {2}, last {3}, cumul {4}, fills {5}, remain {6}", partialFillSimulation, totalSize, split, lastSize, cumulativeQuantity, numberFills, remainingSize);
            while (Math.Abs(remainingSize) > 0 && count < numberFills)
            {
                count++;
                remainingSize -= lastSize;
                if (count >= split)
                {
                    lastSize += remainingSize;
                    remainingSize = 0;
                }
                cumulativeQuantity += lastSize;
                if (remainingSize == 0)
                {
                    CancelBrokerOrder(order.BrokerOrder);
                }
                order.RemainingSize = Math.Abs(remainingSize);
                order.CumulativeSize = Math.Abs(cumulativeQuantity);
                order.RemainingSize = Math.Abs(remainingSize);
                CreateSingleFill(lastSize, totalSize, cumulativeQuantity, remainingSize, price, time, utcTime, order);
            }
        }

        private void CreateSingleFill(int size, int totalSize, int cumulativeSize, int remainingSize, double price, TimeStamp time, TimeStamp utcTime, PhysicalOrder order)
        {
            if (debug) log.DebugFormat("Changing actual position from {0} to {1}. Fill size is {2}", this.actualPosition, (actualPosition + size), size);
            this.actualPosition += size;
            var fill = new PhysicalFillDefault(symbol, size, price, time, utcTime, order.BrokerOrder, createExitStrategyFills, totalSize, cumulativeSize, remainingSize, false, createActualFills);
            if (debug) log.DebugFormat("Enqueuing fill (online: {0}): {1}", isOnline, fill);
            var wrapper = new FillWrapper
                              {
                                  IsCounterSet = isOnline,
                                  Fill = fill,
                                  Order = order,
                              };
            if (enableSyncTicks && wrapper.IsCounterSet) tickSync.AddPhysicalFill(fill);
            fillQueue.Enqueue(wrapper);
        }

        public bool UseSyntheticLimits
        {
            get { return useSyntheticLimits; }
            set { useSyntheticLimits = value; }
        }

        public bool UseSyntheticStops
        {
            get { return useSyntheticStops; }
            set { useSyntheticStops = value; }
        }

        public bool UseSyntheticMarkets
        {
            get { return useSyntheticMarkets; }
            set { useSyntheticMarkets = value; }
        }

        public Action<PhysicalFill,PhysicalOrder> OnPhysicalFill
        {
            get { return onPhysicalFill; }
            set { onPhysicalFill = value; }
        }

        public int GetActualPosition(SymbolInfo symbol)
        {
            return actualPosition;
        }

        public int ActualPosition
        {
            get { return actualPosition; }
            set
            {
                if (actualPosition != value)
                {
                    if (debug) log.DebugFormat("Setter: ActualPosition changed from {0} to {1}", actualPosition, value);
                    actualPosition = value;
                    if (onPositionChange != null)
                    {
                        onPositionChange(actualPosition);
                    }
                }
            }
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public Action<long> OnPositionChange
        {
            get { return onPositionChange; }
            set { onPositionChange = value; }
        }

        public PhysicalOrderConfirm ConfirmOrders
        {
            get { return confirmOrders; }
            set
            {
                confirmOrders = value;
                if (confirmOrders == this)
                {
                    throw new ApplicationException("Please set ConfirmOrders to an object other than itself to avoid circular loops.");
                }
            }
        }

        public bool IsBarData
        {
            get { return isBarData; }
            set { isBarData = value; }
        }

        public Action<PhysicalOrder, string> OnRejectOrder
        {
            get { return onRejectOrder; }
            set { onRejectOrder = value; }
        }

        public TimeStamp CurrentTick
        {
            get { return currentTick.UtcTime; }
        }

        public static int MaxPartialFillsPerOrder
        {
            get { return maxPartialFillsPerOrder; }
            set { maxPartialFillsPerOrder = value; }
        }

        public bool IsOnline
        {
            get { return isOnline; }
            set
            {
                if (isOnline != value)
                {
                    isOnline = value;
                    if (debug) log.DebugFormat("IsOnline changed to {0}", isOnline);
                }
            }
        }

        public PartialFillSimulation PartialFillSimulation
        {
            get { return partialFillSimulation; }
            set { partialFillSimulation = value; }
        }

        public bool EnableSyncTicks
        {
            get { return enableSyncTicks; }
            set { enableSyncTicks = value; }
        }

        private void OrderChanged()
        {
            if (enableSyncTicks && !tickSync.SentOrderChange)
            {
                tickSync.AddOrderChange();
            }
        }

        private void ClearOrderChanged()
        {
            if (enableSyncTicks && tickSync.SentOrderChange)
            {
                tickSync.RemoveOrderChange();
            }
        }

        public bool IsChanged
        {
            get { return isChanged; }
            set
            {
                if (isChanged != value)
                {
                    //if (debug) log.Debug("IsChanged from " + isChanged + " to " + value);
                    isChanged = value;
                }
            }
        }
    }
}
