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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

using TickZoom.Api;

namespace TickZoom.Common
{
    public class OrderAlgorithmDefault : OrderAlgorithm, LogAware {
		private static readonly Log staticLog = Factory.SysLog.GetLogger(typeof(OrderAlgorithmDefault));
        private volatile bool trace = staticLog.IsTraceEnabled;
        private volatile bool debug = staticLog.IsDebugEnabled;
        public void RefreshLogLevel()
        {
            if (log != null)
            {
                debug = log.IsDebugEnabled;
                trace = log.IsTraceEnabled;
            }
        }
        private bool DoSyncTicks
        {
            get { return enableSyncTicks && !handleSimulatedExits; }
        }
        private Log log;
		private SymbolInfo symbol;
		private PhysicalOrderHandler physicalOrderHandler;
        private PhysicalOrderHandler syntheticOrderHandler;
        private volatile bool bufferedLogicalsChanged = false;
        private List<PhysicalOrder> originalPhysicals;
        private List<PhysicalOrder> physicalOrders;
        private List<LogicalOrder> bufferedLogicals;
        private List<LogicalOrder> originalLogicals;
        private List<LogicalOrder> logicalOrders;
        private List<LogicalOrder> extraLogicals;
		private int desiredPosition;
		private Action<SymbolInfo,LogicalFillBinary> onProcessFill;
        private Action<SymbolInfo,LogicalTouch> onProcessTouch;
        private bool handleSimulatedExits = false;
		private TickSync tickSync;
	    private LogicalOrderCache logicalOrderCache;
        private bool isPositionSynced = false;
        private long minimumTick;
        private List<MissingLevel> missingLevels = new List<MissingLevel>();
        private PhysicalOrderCache physicalOrderCache;
        private long recency;
        private string name;
        private bool enableSyncTicks;
        private int rejectRepeatCounter;
        private int confirmedOrderCount;
        private bool isBrokerOnline;
        private bool receivedDesiredPosition;
        private bool disableChangeOrders;

        public class OrderArray<T>
        {
            private int capacity = 16;
            private T[] orders;
            public OrderArray()
            {
                orders = new T[capacity];
            }
        }

        public struct MissingLevel
        {
            public int Size;
            public long Price;
        }

        public OrderAlgorithmDefault(string name, SymbolInfo symbol, PhysicalOrderHandler brokerOrders, PhysicalOrderHandler syntheticOrders, LogicalOrderCache logicalOrderCache, PhysicalOrderCache physicalOrderCache)
        {
            log = Factory.SysLog.GetLogger(typeof(OrderAlgorithmDefault).FullName + "." + name + "." + symbol.ExpandedSymbol.StripInvalidPathChars());
            log.Register(this);
			this.symbol = symbol;
		    this.logicalOrderCache = logicalOrderCache;
		    this.physicalOrderCache = physicalOrderCache;
		    this.name = name;
			tickSync = SyncTicks.GetTickSync(symbol.BinaryIdentifier);
			this.physicalOrderHandler = brokerOrders;
            this.syntheticOrderHandler = syntheticOrders;
            this.originalLogicals = new List<LogicalOrder>();
            this.bufferedLogicals = new List<LogicalOrder>();
            this.logicalOrders = new List<LogicalOrder>();
            this.originalPhysicals = new List<PhysicalOrder>();
            this.physicalOrders = new List<PhysicalOrder>();
            this.extraLogicals = new List<LogicalOrder>();
            this.minimumTick = symbol.MinimumTick.ToLong();
            if( debug) log.DebugFormat(LogMessage.LOGMSG423, recency);
		}

        public bool PositionChange( PositionChangeDetail positionChange, bool isRecovered)
        {
            if( positionChange.Recency < recency)
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG424, positionChange.Recency, recency);
                if( DoSyncTicks)
                {
                    if (!tickSync.SentWaitingMatch)
                    {
                        tickSync.AddWaitingMatch("StalePositionChange");
                    }
                    tickSync.RemovePositionChange(name);
                }
                return false;
            }
            if (debug) log.DebugFormat(LogMessage.LOGMSG425, positionChange);
            recency = positionChange.Recency;
            SetDesiredPosition(positionChange.Position);
            SetStrategyPositions(positionChange.StrategyPositions);
            SetLogicalOrders(positionChange.Orders);
            if (isRecovered)
            {
                TrySyncPosition(positionChange.StrategyPositions);
                PerformCompareProtected();
            }
            else
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG426);
                if (DoSyncTicks && isBrokerOnline)
                {
                    if (!tickSync.SentWaitingMatch)
                    {
                        tickSync.AddWaitingMatch("PositionChange");
                    }
                }
            }
            if (DoSyncTicks)
            {
                tickSync.RemovePositionChange(name);
            }
            return true;
        }
		
		private List<PhysicalOrder> TryMatchId( IEnumerable<PhysicalOrder> list, LogicalOrder logical)
		{
            var physicalOrderMatches = new List<PhysicalOrder>();
            foreach (var physical in list)
		    {
				if( logical.SerialNumber == physical.LogicalSerialNumber ) {
                    switch( physical.OrderState)
                    {
                        case OrderState.Suspended:
                            if (debug) log.DebugFormat(LogMessage.LOGMSG427, physical);
                            break;
                        case OrderState.Filled:
                            if (debug) log.DebugFormat(LogMessage.LOGMSG428, physical);
                            break;
                        default:
                            if( physical.ReplacedBy == null)
                            {
                                physicalOrderMatches.Add(physical);
                            }
                            break;
                    }
				}
			}
			return physicalOrderMatches;
		}

        private bool TryCancelBrokerOrder(PhysicalOrder physical)
        {
            return TryCancelBrokerOrder(physical, false);
        }

        private bool TryCancelBrokerOrder(PhysicalOrder physical, bool forStaleOrder)
        {
			bool result = false;
            if (!physical.IsPending)
            {
                result = Cancel(physical,forStaleOrder);
            }
            return result;
        }

        private bool CancelStale( PhysicalOrder physical)
        {
            return Cancel(physical, true);
        }
		
        private bool Cancel(PhysicalOrder physical, bool forStaleOrder)
        {
			var result = false;
            var cancelOrder = physicalOrderCache.Create();
            cancelOrder.Initialize(OrderState.Pending, symbol, physical);
            if (physicalOrderCache.HasCancelOrder(cancelOrder))
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG429, physical.BrokerOrder);
                return result;
            }
            if (rejectRepeatCounter > 1)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG430);
                return result;
            }
            physical.ReplacedBy = cancelOrder;
            if (debug) log.DebugFormat(LogMessage.LOGMSG431, cancelOrder);
            physicalOrderCache.SetOrder(cancelOrder);
            if( !forStaleOrder)
            {
                TryAddPhysicalOrder(cancelOrder);
            }
            if( cancelOrder.IsSynthetic)
            {
                if (syntheticOrderHandler.OnCancelBrokerOrder(cancelOrder))
                {
                    physical.CancelCount++;
                    result = true;
                }
            }
            else
            {
                if (physicalOrderHandler.OnCancelBrokerOrder(cancelOrder))
                {
                    physical.CancelCount++;
                    result = true;
                }
            }
            if( !result)
            {
                if( !forStaleOrder)
                {
                    TryRemovePhysicalOrder(cancelOrder);
                }
                physicalOrderCache.RemoveOrder(cancelOrder.BrokerOrder);
            }
		    return result;
		}

		private void TryChangeBrokerOrder(PhysicalOrder physical, PhysicalOrder origOrder, LogicalOrder logical) {
            if (DisableChangeOrders)
            {
                TryCancelBrokerOrder(origOrder);
                return;
            }
            if (physical.IsSynthetic && !origOrder.IsSynthetic)
            {
                throw new ApplicationException("A synthetic fill order cannot be changed (which is a market order) only canceled: " + origOrder);
            }
            if (origOrder.OrderState == OrderState.Active)
            {
                physical.Side = origOrder.Side;
                physical.OriginalOrder = origOrder;
                if (physicalOrderCache.HasCancelOrder(physical))
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG432, origOrder.BrokerOrder);
                    return;
                }
	            if (rejectRepeatCounter > 1)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG430);
                    return;
                }
                origOrder.ReplacedBy = physical;
                if (logical != null && logical.Type == OrderType.Stop && logical.IsTouched)
                {
                    // After logical stops are "touched" by getting any physical or synthetic fill
                    // then any additional physical orders for that stop must be market orders.
                    physical.Type = OrderType.Market;
                    physical.IsSynthetic = false;
                    physical.IsTouch = true;
                }
                if (debug) log.DebugFormat(LogMessage.LOGMSG433, physical);
                TryAddPhysicalOrder(physical);
                physicalOrderCache.SetOrder(physical);
                if( physical.IsSynthetic)
                {
                    if (!syntheticOrderHandler.OnChangeBrokerOrder(physical))
                    {
                        physicalOrderCache.RemoveOrder(physical.BrokerOrder);
                        TryRemovePhysicalOrder(physical);
                    }
                }
                else
                {
                    if (!physicalOrderHandler.OnChangeBrokerOrder(physical))
                    {
                        physicalOrderCache.RemoveOrder(physical.BrokerOrder);
                        TryRemovePhysicalOrder(physical);
                    }
                }
            }
		}
		
		private void TryAddPhysicalOrder(PhysicalOrder physical) {
            if (DoSyncTicks) tickSync.AddPhysicalOrder(physical);
		}

        private void TryRemovePhysicalOrder(PhysicalOrder physical)
        {
            if (DoSyncTicks) tickSync.RemovePhysicalOrder(physical);
        }

        private bool TryCreateBrokerOrder(PhysicalOrder physical, LogicalOrder logical)
        {
			if( debug) log.DebugFormat(LogMessage.LOGMSG434, physical);
            if (physical.RemainingSize <= 0)
            {
                throw new ApplicationException("Sorry, order size must be greater than or equal to zero.");
            }
            if (physicalOrderCache.HasCreateOrder(physical))
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG435);
                return false;
            }
            if (rejectRepeatCounter > 1)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG430);
                return false;
            }
            if( logical != null && logical.Type == OrderType.Stop && logical.IsTouched)
            {
                // After logical stops are "touched" by getting any physical or synthetic fill
                // then any additional physical orders for that stop must be market orders.
                physical.Type = OrderType.Market;
                physical.IsSynthetic = false;
                physical.IsTouch = true;
                physical.UtcCreateTime = logical.UtcTouchTime;
            }
            TryAddPhysicalOrder(physical);
            physicalOrderCache.SetOrder(physical);
            if (physical.IsSynthetic)
            {
                if (!syntheticOrderHandler.OnCreateBrokerOrder(physical))
                {
                    physicalOrderCache.RemoveOrder(physical.BrokerOrder);
                    TryRemovePhysicalOrder(physical);
                }
            }
            else
            {
                if (!physicalOrderHandler.OnCreateBrokerOrder(physical))
                {
                    physicalOrderCache.RemoveOrder(physical.BrokerOrder);
                    TryRemovePhysicalOrder(physical);
                }
            }
            return true;
        }

        private string ToString(List<PhysicalOrder> matches)
        {
            var sb = new StringBuilder();
            foreach( var physical in matches)
            {
                sb.AppendLine(physical.ToString());
            }
            return sb.ToString();
        }

        public virtual bool ProcessMatchPhysicalEntry(LogicalOrder logical, List<PhysicalOrder> matches)
		{
            if (matches.Count != 1)
            {
                log.Warn("Expected 1 match but found " + matches.Count + " matches for logical order: " + logical + "\n" + ToString(matches));
            }
            physicalOrders.Remove(matches[0]);
            return ProcessMatchPhysicalEntry(logical, matches[0], logical.Position, logical.Price);
		}

        protected bool ProcessMatchPhysicalEntry(LogicalOrder logical, PhysicalOrder physical, int position, double price)
        {
            var result = true;
			log.TraceFormat(LogMessage.LOGMSG436);
			var strategyPosition = GetStrategyPosition(logical);
            var difference = position - Math.Abs(strategyPosition);
			log.TraceFormat(LogMessage.LOGMSG437, difference);
			if( difference == 0)
			{
			    result = false;
				TryCancelBrokerOrder(physical);
			}
            else if (logical.IsSynthetic && !physical.IsSynthetic)
            {
                // This is a synthetic fill which means a market order waiting to fill
                // so the order in cannot change in below conditions.
            }
            else if (difference != physical.RemainingSize)
			{
			    result = false;
			    if (strategyPosition == 0)
			    {
			        physicalOrders.Remove(physical);
			        var side = GetOrderSide(logical.Side);
			        var changeOrder = physicalOrderCache.Create();
                    changeOrder.Initialize(OrderAction.Change, symbol, logical, side, difference, physical.CumulativeSize, price);
			        TryChangeBrokerOrder(changeOrder, physical, logical);
			    }
			    else
			    {
			        if (strategyPosition > 0)
			        {
			            if (logical.Side == OrderSide.Buy)
			            {
			                physicalOrders.Remove(physical);
			                var side = GetOrderSide(logical.Side);
			                var changeOrder = physicalOrderCache.Create();
                            changeOrder.Initialize(OrderAction.Change, symbol, logical, side, difference, physical.CumulativeSize, price);
			                TryChangeBrokerOrder(changeOrder, physical, logical);
			            }
			            else
			            {
			                if (debug)
			                    log.DebugFormat(LogMessage.LOGMSG438, strategyPosition, logical.Type);
			                TryCancelBrokerOrder(physical);
			            }
			        }
			        if (strategyPosition < 0)
			        {
			            if (logical.Side == OrderSide.Sell)
			            {
			                physicalOrders.Remove(physical);
			                var side = GetOrderSide(logical.Side);
			                var changeOrder = physicalOrderCache.Create();
                            changeOrder.Initialize(OrderAction.Change, symbol, logical, side, difference, physical.CumulativeSize, price);
			                TryChangeBrokerOrder(changeOrder, physical, logical);
			            }
			            else
			            {
			                if (debug) log.DebugFormat(LogMessage.LOGMSG439, strategyPosition, logical.Type);
			                TryCancelBrokerOrder(physical);
			            }
			        }
			    }
			}
            else if( price.ToLong() != physical.Price.ToLong())
            {
                result = false;
                physicalOrders.Remove(physical);
				var side = GetOrderSide(logical.Side);
                var changeOrder = physicalOrderCache.Create();
                changeOrder.Initialize(OrderAction.Change, symbol, logical, side, difference, physical.CumulativeSize, price);
                TryChangeBrokerOrder(changeOrder, physical, logical);
            }
            else
            {
				result = VerifySide( logical, physical, price);
			}
            return result;
        }

        private bool ProcessMatchPhysicalReverse(LogicalOrder logical, List<PhysicalOrder> matches)
        {
            if (matches.Count != 1)
            {
                log.Warn("Expected 1 match but found " + matches.Count + " matches for logical order: " + logical + "\n" + ToString(matches));
            }
            physicalOrders.Remove(matches[0]);
            var physical = matches[0];
            return ProcessMatchPhysicalReverse(logical, physical, logical.Position, logical.Price);
        }

        private bool ProcessMatchPhysicalReverse(LogicalOrder logical, PhysicalOrder physical, int position, double price)
        {
            var result = true;
            var strategyPosition = GetStrategyPosition(logical);
            var logicalPosition = logical.Side  == OrderSide.Buy ? position : - position;
			var physicalPosition = physical.Side == OrderSide.Buy ? physical.RemainingSize : - physical.RemainingSize;
			var delta = logicalPosition - strategyPosition;
			var difference = delta - physicalPosition;
			if( delta == 0 || (logicalPosition > 0 && strategyPosition > logicalPosition) ||
			  (logicalPosition < 0 && strategyPosition < logicalPosition))
			{
			    result = false;
			    TryCancelBrokerOrder(physical);
			}
            else if (logical.IsSynthetic && !physical.IsSynthetic)
            {
                // This is a synthetic fill which means a market order waiting to fill
                // so the order cannot change in below conditions.
            }
			else if( difference != 0)
            {
                result = false;
				if( delta > 0)
				{
				    var changeOrder = physicalOrderCache.Create();
                    changeOrder.Initialize(OrderAction.Change, symbol, logical, OrderSide.Buy, Math.Abs(delta), physical.CumulativeSize, price);
                    TryChangeBrokerOrder(changeOrder, physical, logical);
                }
                else
                {
					if( strategyPosition > 0 && logicalPosition < 0) {
						delta = strategyPosition;
						if( delta == physical.RemainingSize) {
							result = ProcessMatchPhysicalChangePriceAndSide( logical, physical, delta, price);
							return result;
						}
					}
					var side = (long) strategyPosition >= (long) Math.Abs(delta) ? OrderSide.Sell : OrderSide.SellShort;
                    var changeOrder = physicalOrderCache.Create();
                    changeOrder.Initialize(OrderAction.Change, symbol, logical, side, Math.Abs(delta), physical.CumulativeSize, price);
                    TryChangeBrokerOrder(changeOrder, physical, logical);
                }
			} else {
				result = ProcessMatchPhysicalChangePriceAndSide( logical, physical, delta, price);
			}
            return result;
        }
		
        private bool MatchLogicalToPhysicals(LogicalOrder logical, List<PhysicalOrder> matches, Func<LogicalOrder, PhysicalOrder, int, double, bool> onMatchCallback){
            var result = true;
            var price = logical.Price.ToLong();
            var sign = 1;
            var sellStop = logical.Side != OrderSide.Buy && logical.Type == OrderType.Stop;
            var buyLimit = logical.Side == OrderSide.Buy && logical.Type == OrderType.Limit;
            switch (logical.Type)
            {
                case OrderType.Market:
                    break;
                case OrderType.Limit:
                case OrderType.Stop:
					if( sellStop || buyLimit)
                    {
                        sign = -1;
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unknown logical order type: " + logical.Type);

            }
            missingLevels.Clear();
            var levelSize = logical.Levels == 1 ? logical.Position : logical.LevelSize;
            var logicalPosition = logical.Position;
            var level = logical.Levels - 1;
            for (var i = 0; i < logical.Levels; i++, level --, logicalPosition -= levelSize)
            {
                var size = Math.Min(logicalPosition,levelSize) ;
                if( size == 0) break;
                var levelPrice = price + sign*minimumTick*logical.LevelIncrement*level;
                // Find a match.
                var matched = false;
                for (var j = 0; j < matches.Count; j++)
                {
                    var physical = matches[j];
                    physicalOrders.Remove(physical);
                    if (physical.Price.ToLong() != levelPrice) continue;
                    if( !onMatchCallback(logical, physical, size, levelPrice.ToDouble()))
                    {
                        result = false;
                    }
                    matches.RemoveAt(j);
                    matched = true;
                    break;
                }
                if (!matched)
                {
                    missingLevels.Add(new MissingLevel { Price = levelPrice, Size = size });
                }
            }
            for (var i = 0; i < matches.Count; i++)
            {
                var physical = matches[i];
                if( missingLevels.Count > 0)
                {
                    var missingLevel = missingLevels[0];
                    if( !onMatchCallback(logical, physical, missingLevel.Size, missingLevel.Price.ToDouble()))
                    {
                        result = false;
                    }
                    missingLevels.RemoveAt(0);
                }
                else
                {
                    TryCancelBrokerOrder(physical);
                }

            }
            for (var i = 0; i < missingLevels.Count; i++ ) 
            {
                result = false;
                var missingLevel = missingLevels[i];
                ProcessMissingPhysical(logical, missingLevel.Size, missingLevel.Price.ToDouble());
            }
            return result;
        }

        private bool ProcessMatchPhysicalChange(LogicalOrder logical, List<PhysicalOrder> matches)
        {
            if (logical.Levels == 1)
            {
                if (matches.Count != 1)
                {
                    log.Warn("Expected 1 match but found " + matches.Count + " matches for logical order: " + logical + "\n" + ToString(matches));
                }
                physicalOrders.Remove(matches[0]);
                var physical = matches[0];
                return ProcessMatchPhysicalChange(logical, physical, logical.Position, logical.Price);
            }
            return MatchLogicalToPhysicals(logical, matches, ProcessMatchPhysicalChange);
        }

        private bool ProcessMatchPhysicalChange(LogicalOrder logical, PhysicalOrder physical, int position, double price)
        {
            var result = true;
            var strategyPosition = GetStrategyPosition(logical);
            var logicalPosition = logical.Side == OrderSide.Buy ? position : - position;
			logicalPosition += strategyPosition;
			var physicalPosition = physical.Side == OrderSide.Buy ? physical.RemainingSize : - physical.RemainingSize;
			var delta = logicalPosition - strategyPosition;
			var difference = delta - physicalPosition;
			if( debug) log.DebugFormat(LogMessage.LOGMSG440, logical.SerialNumber, delta, strategyPosition, difference);
			if( delta == 0)
			{
			    if (debug) log.DebugFormat(LogMessage.LOGMSG441, physical);
			    result = false;
			    TryCancelBrokerOrder(physical);
			}
			else if (logical.IsSynthetic && !physical.IsSynthetic)
            {
                // This is a synthetic fill which means a market order waiting to fill
                // so the order in cannot change in below conditions.
			}
            else if( difference != 0)
            {
                result = false;
                var origBrokerOrder = physical.BrokerOrder;
				if( delta > 0)
				{
				    var changeOrder = physicalOrderCache.Create();
                    changeOrder.Initialize(OrderAction.Change, symbol, logical, OrderSide.Buy, Math.Abs(delta), physical.CumulativeSize, price);
					if( debug) log.DebugFormat(LogMessage.LOGMSG442, origBrokerOrder, changeOrder);
                    TryChangeBrokerOrder(changeOrder, physical, logical);
                }
                else
                {
					OrderSide side;
					if( strategyPosition > 0 && logicalPosition < 0) {
						side = OrderSide.Sell;
						delta = strategyPosition;
						if( delta == physical.RemainingSize) {
							if( debug) log.DebugFormat(LogMessage.LOGMSG443);
							ProcessMatchPhysicalChangePriceAndSide(logical,physical,delta,price);
							return result;
						}
					}
					side = strategyPosition >= Math.Abs(delta) ? OrderSide.Sell : OrderSide.SellShort;
					if( side == physical.Side)
					{
					    var changeOrder = physicalOrderCache.Create();
                        changeOrder.Initialize(OrderAction.Change, symbol, logical, side, Math.Abs(delta), physical.CumulativeSize, price);
						if( debug) log.DebugFormat(LogMessage.LOGMSG444, origBrokerOrder, changeOrder);
                        TryChangeBrokerOrder(changeOrder, physical, logical);
                    }
                    else
                    {
						if( debug) log.DebugFormat(LogMessage.LOGMSG445, physical);
						TryCancelBrokerOrder(physical);
					}
				}
		    } else {
				result = ProcessMatchPhysicalChangePriceAndSide(logical,physical,delta,price);
			}
            return result;
        }
		
		private bool ProcessMatchPhysicalChangePriceAndSide(LogicalOrder logical, PhysicalOrder physical, int delta, double price)
		{
		    var result = true;
			if( price.ToLong() != physical.Price.ToLong())
			{
			    result = false;
				var origBrokerOrder = physical.BrokerOrder;
				physicalOrders.Remove(physical);
				var side = GetOrderSide(logical.Side);
				if( side == physical.Side)
				{
				    var changeOrder = physicalOrderCache.Create();
                    changeOrder.Initialize(OrderAction.Change, symbol, logical, side, Math.Abs(delta), physical.CumulativeSize, price);
					if( debug) log.DebugFormat(LogMessage.LOGMSG446, origBrokerOrder, changeOrder);
                    TryChangeBrokerOrder(changeOrder, physical, logical);
                }
                else
                {
					if( debug) log.DebugFormat(LogMessage.LOGMSG447, physical);
					TryCancelBrokerOrder(physical);
				}
			} else {
				result = VerifySide( logical, physical, price);
			}
		    return result;
		}

        private bool ProcessMatchPhysicalExit(LogicalOrder logical, List<PhysicalOrder> matches)
        {
            if (matches.Count != 1)
            {
                log.Warn("Expected 1 match but found " + matches.Count + " matches for logical order: " + logical + "\n" + ToString(matches));
            }
            var physical = matches[0];
            physicalOrders.Remove(matches[0]);
            return ProcessMatchPhysicalExit(logical, physical, logical.Position, logical.Price);
        }

        private bool ProcessMatchPhysicalExit(LogicalOrder logical, PhysicalOrder physical, int position, double price)
        {
            var result = true;
            var strategyPosition = GetStrategyPosition(logical);
            if (strategyPosition == 0)
            {
                result = false;
                TryCancelBrokerOrder(physical);
            }
            else if (logical.IsSynthetic && !physical.IsSynthetic)
            {
                // This is a synthetic fill which means a market order waiting to fill
                // so the order in cannot change in below conditions.
            }
			else if( Math.Abs(strategyPosition) != physical.RemainingSize || price.ToLong() != physical.Price.ToLong())
            {
                result = false;
				physicalOrders.Remove(physical);
				var side = GetOrderSide(logical.Side);
                var changeOrder = physicalOrderCache.Create();
                changeOrder.Initialize(OrderAction.Change, symbol, logical, side, Math.Abs(strategyPosition), physical.CumulativeSize, price);
                TryChangeBrokerOrder(changeOrder, physical, logical);
            }
            else
            {
				result = VerifySide( logical, physical, price);
			}
            return result;
        }

        private bool ProcessMatchPhysicalExitStrategy(LogicalOrder logical, List<PhysicalOrder> matches)
        {
            if( logical.Levels == 1) {
                if (matches.Count != 1)
                {
                    log.Warn("Expected 1 match but found " + matches.Count + " matches for logical order: " + logical + "\n" + ToString(matches));
                }
                physicalOrders.Remove(matches[0]);
                var physical = matches[0];
                return ProcessMatchPhysicalExitStrategy(logical,physical,logical.Position,logical.Price);
            }
            return MatchLogicalToPhysicals(logical, matches, ProcessMatchPhysicalExitStrategy);
        }

        private bool ProcessMatchPhysicalExitStrategy(LogicalOrder logical, PhysicalOrder physical, int position, double price)
        {
            var result = true;
            var strategyPosition = GetStrategyPosition(logical);
            if (strategyPosition == 0)
			{
			    result = false;
				TryCancelBrokerOrder(physical);
            }
            else if (logical.IsSynthetic && !physical.IsSynthetic)
            {
                // This is a synthetic fill which means a market order waiting to fill
                // so the order in cannot change in below conditions.
            }
			else if( Math.Abs(strategyPosition) != physical.RemainingSize || price.ToLong() != physical.Price.ToLong())
            {
                result = false;
                var origBrokerOrder = physical.BrokerOrder;
				physicalOrders.Remove(physical);
				var side = GetOrderSide(logical.Side);
                var changeOrder = physicalOrderCache.Create();
                changeOrder.Initialize(OrderAction.Change, symbol, logical, side, Math.Abs(strategyPosition), physical.CumulativeSize, price);
                TryChangeBrokerOrder(changeOrder, physical, logical);
            }
            else
            {
				result = VerifySide( logical, physical, price);
			}
            return result;
        }

        private SimpleLock matchLocker = new SimpleLock();
		private bool ProcessMatch(LogicalOrder logical, List<PhysicalOrder> matches)
		{
		    var result = false;
		    if( !matchLocker.TryLock()) return false;
            try
		    {
			    if( trace) log.TraceFormat(LogMessage.LOGMSG448);
			    switch( logical.TradeDirection) {
				    case TradeDirection.Entry:
					    result = ProcessMatchPhysicalEntry( logical, matches);
					    break;
				    case TradeDirection.Exit:
                        result = ProcessMatchPhysicalExit(logical, matches);
					    break;
				    case TradeDirection.ExitStrategy:
                        result = ProcessMatchPhysicalExitStrategy(logical, matches);
					    break;
				    case TradeDirection.Reverse:
                        result = ProcessMatchPhysicalReverse(logical, matches);
					    break;
				    case TradeDirection.Change:
                        result = ProcessMatchPhysicalChange(logical, matches);
					    break;
				    default:
					    throw new ApplicationException("Unknown TradeDirection: " + logical.TradeDirection);
			    }
		    } finally
            {
                matchLocker.Unlock();
            }
		    return result;
		}

		private bool VerifySide( LogicalOrder logical, PhysicalOrder physical, double price)
		{
		    var result = true;
#if VERIFYSIDE
			var side = GetOrderSide(logical.Type);
			if( createOrChange.Side != side && ( createOrChange.Type != OrderType.BuyMarket && createOrChange.Type != OrderType.SellMarket)) {
                if (debug) log.Debug("Cancel because " + createOrChange.Side + " != " + side + ": " + createOrChange);
				if( TryCancelBrokerOrder(createOrChange))
				{
                    createOrChange = new CreateOrChangeOrderDefault(OrderState.Pending, symbol, logical, side, createOrChange.Size, price);
                    TryCreateBrokerOrder(createOrChange);
                    result = false;
                }
			}
#endif
		    return result;
		}

        private int GetStrategyPosition(LogicalOrder logical)
        {
            var strategyPosition = (int)physicalOrderCache.GetStrategyPosition(logical.StrategyId);
            if (handleSimulatedExits)
            {
                strategyPosition = logical.StrategyPosition;
            }
            return strategyPosition;
        }
		
		private bool ProcessExtraLogical(LogicalOrder logical)
		{
		    var result = true;
            // When flat, allow entry orders.
			switch(logical.TradeDirection) {
				case TradeDirection.Entry:
    				result = ProcessMissingPhysical(logical);
					break;
				case TradeDirection.Exit:
				case TradeDirection.ExitStrategy:
                    if (GetStrategyPosition(logical)!= 0)
                    {
                        result = ProcessMissingPhysical(logical);
					}
					break;
				case TradeDirection.Reverse:
                    result = ProcessMissingPhysical(logical);
					break;
				case TradeDirection.Change:
                    result = ProcessMissingPhysical(logical);
					break;
				default:
					throw new ApplicationException("Unknown trade direction: " + logical.TradeDirection);
			}
		    return result;
		}

        private bool ProcessMissingPhysical(LogicalOrder logical)
        {
            var result = true;
            if( logical.Levels == 1)
            {
                result = ProcessMissingPhysical(logical, logical.Position, logical.Price);
                return result;
            }
            var price = logical.Price.ToLong();
            var sign = 1;
            var sellStop = logical.Side != OrderSide.Buy && logical.Type == OrderType.Stop;
            var buyLimit = logical.Side == OrderSide.Buy && logical.Type == OrderType.Limit;
            switch (logical.Type)
            {
                case OrderType.Market:
                    result = ProcessMissingPhysical(logical, logical.Position, logical.Price);
                    return result;
                case OrderType.Limit:
                case OrderType.Stop:
                    if( sellStop || buyLimit)
                    {
                        sign = -1;
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unknown logical order type: " + logical.Type);

            }
            var logicalPosition = logical.Position;
            var level = sign > 0 ? logical.Levels-1 : 0;
            for( var i=0; i< logical.Levels; i++, level-=sign)
            {
                var size = Math.Min(logical.LevelSize, logicalPosition);
                var levelPrice = price + sign * minimumTick * logical.LevelIncrement * level;
                if( !ProcessMissingPhysical(logical, size, levelPrice.ToDouble()))
                {
                    result = false;
                }
                logicalPosition -= logical.LevelSize;
            }
            return result;
        }

        private bool ProcessMissingPhysical(LogicalOrder logical, int position, double price)
        {
            var result = true;
            var logicalPosition = logical.Side == OrderSide.Buy ? position : -position;
            var strategyPosition = GetStrategyPosition(logical);
            var size = Math.Abs(logicalPosition - strategyPosition);
            switch (logical.TradeDirection)
            {
				case TradeDirection.Entry:
					if(debug) log.DebugFormat(LogMessage.LOGMSG449, logical);
                    var side = GetOrderSide(logical.Side);
                    if (logicalPosition < 0 && strategyPosition <= 0 && strategyPosition > logicalPosition)
                    {
                        result = false;
                        var physical = physicalOrderCache.Create();
                        physical.Initialize(OrderState.Pending, symbol, logical, side, size, 0, price);
                        TryCreateBrokerOrder(physical, logical);
                    }
                    if (logicalPosition > 0 && strategyPosition >= 0 && strategyPosition < logicalPosition)
                    {
                        result = false;
                        var physical = physicalOrderCache.Create();
                        physical.Initialize(OrderState.Pending, symbol, logical, side, size, 0, price);
                        TryCreateBrokerOrder(physical, logical);
                    }
					break;
				case TradeDirection.Exit:
				case TradeDirection.ExitStrategy:
                    size = Math.Abs(strategyPosition);
					result = ProcessMissingExit( logical, size, price);
					break;
				case TradeDirection.Reverse:
                    result = ProcessMissingReverse(logical, size, price, logicalPosition);
                    break;
				case TradeDirection.Change:
					logicalPosition += strategyPosition;
					size = Math.Abs(logicalPosition - strategyPosition);
					if( size != 0) {
						if(debug) log.DebugFormat(LogMessage.LOGMSG450, logical);
					    result = false;
						side = GetOrderSide(logical.Side);
					    var physical = physicalOrderCache.Create();
                        physical.Initialize(OrderState.Pending, symbol, logical, side, size, 0, price);
						TryCreateBrokerOrder(physical, logical);
					}
					break;
				default:
					throw new ApplicationException("Unknown trade direction: " + logical.TradeDirection);
			}
            return result;
        }

        private bool ProcessMissingReverse(LogicalOrder logical, int size, double price, int logicalPosition)
        {
            var result = true;
            if (size == 0) return result;
            if (debug) log.DebugFormat(LogMessage.LOGMSG451, logical);
            var strategyPosition = GetStrategyPosition(logical);
            var delta = logicalPosition - strategyPosition;
            if (delta == 0 || (logicalPosition > 0 && strategyPosition > logicalPosition) ||
              (logicalPosition < 0 && strategyPosition < logicalPosition))
            {
                return result;
            }
            if (delta > 0)
            {
                result = false;
                var createOrder = physicalOrderCache.Create();
                createOrder.Initialize(OrderAction.Create, symbol, logical, OrderSide.Buy, Math.Abs(delta), 0, price);
                TryCreateBrokerOrder(createOrder, logical);
            }
            else
            {
                if (strategyPosition > 0 && logicalPosition < 0)
                {
                    delta = strategyPosition;
                }
                result = false;
                var side = strategyPosition >= Math.Abs(delta) ? OrderSide.Sell : OrderSide.SellShort;
                var createOrder = physicalOrderCache.Create();
                createOrder.Initialize(OrderAction.Create, symbol, logical, side, Math.Abs(delta), 0, price);
                TryCreateBrokerOrder(createOrder, logical);
            }
            return result;
        }

        private bool ProcessMissingExit(LogicalOrder logical, int size, double price)
        {
            var result = true;
            var strategyPosition = GetStrategyPosition(logical);
            if (strategyPosition > 0)
            {
                if (logical.Side == OrderSide.Sell)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG452, strategyPosition, logical);
                    result = false;
                    var side = GetOrderSide(logical.Side);
                    var physical = physicalOrderCache.Create();
                    physical.Initialize(OrderState.Pending, symbol, logical, side, size, 0, price);
                    TryCreateBrokerOrder(physical, logical);
                }
			}
			if( strategyPosition < 0) {
                if (logical.Side == OrderSide.Buy)
                {
                    result = false;
                    if (debug) log.DebugFormat(LogMessage.LOGMSG452, strategyPosition, logical);
                    var side = GetOrderSide(logical.Side);
                    var physical = physicalOrderCache.Create();
                    physical.Initialize(OrderState.Pending, symbol, logical, side, size, 0, price);
                    TryCreateBrokerOrder(physical, logical);
                }
			}
            return result;
        }

        private bool CheckFilledOrder(LogicalOrder logical, int position)
        {
            var strategyPosition = GetStrategyPosition(logical);
            switch (logical.Side)
            {
                case OrderSide.Buy:
                    switch (logical.TradeDirection)
                    {
                        case TradeDirection.Entry:
                        case TradeDirection.Reverse:
                            return position >= logical.Position;
                        case TradeDirection.Change:
                            return position >= logical.Position + strategyPosition;
                        case TradeDirection.Exit:
                        case TradeDirection.ExitStrategy:
                            if( debug) log.DebugFormat( "Check exit order filled. order id {0}, Side {1}, strategy position {2}, position {3}, logical position {4}", logical.SerialNumber, logical.Side, strategyPosition, position, logical.Position);
                            return strategyPosition >= 0;
                        default:
                            throw new ArgumentOutOfRangeException("Unexpected trade direction: " + logical.TradeDirection);
                    }
                default:
                    switch (logical.TradeDirection)
                    {
                        case TradeDirection.Entry:
                        case TradeDirection.Reverse:
                            return position <= -logical.Position;
                        case TradeDirection.Change:
                            return position <= -logical.Position + strategyPosition;
                        case TradeDirection.Exit:
                        case TradeDirection.ExitStrategy:
                            if( debug) log.DebugFormat( "Check exit order filled. order id {0}, Side {1}, strategy position {2}, position {3}, logical position {4}", logical.SerialNumber, logical.Side, strategyPosition, position, logical.Position);
                            return strategyPosition <= 0;
                        default:
                            throw new ArgumentOutOfRangeException("Unexpected trade direction: " + logical.TradeDirection);
                    }
            }
        }
		
		private OrderSide GetOrderSide(OrderSide side) {
			switch( side) {
				case OrderSide.Buy:
					return OrderSide.Buy;
                default:
                    if (physicalOrderCache.GetActualPosition(symbol) > 0)
                    {
						return OrderSide.Sell;
					} else {
						return OrderSide.SellShort;
					}
			}
		}
		
        private List<PhysicalOrder> tempActiveOrders = new List<PhysicalOrder>();
		private int FindPendingAdjustments() {
            var positionDelta = desiredPosition - physicalOrderCache.GetActualPosition(symbol);
			var pendingAdjustments = 0;

            originalPhysicals.Clear();
		    physicalOrderCache.GetActiveOrders(tempActiveOrders, symbol);
            for (var i = 0; i < tempActiveOrders.Count; i++ )
            {
                originalPhysicals.Add(tempActiveOrders[i]);
            }

            for (var i = 0; i < originalPhysicals.Count; i++ )
            {
                PhysicalOrder order = originalPhysicals[i];
                if (order.Type != OrderType.Market)
                {
                    continue;
                }
                switch (order.OrderState)
                {
                    case OrderState.Filled:
                        continue;
                    case OrderState.Active:
                    case OrderState.Pending:
                    case OrderState.PendingNew:
                    case OrderState.Expired:
                    case OrderState.Suspended:
                        break;
                    default:
                        throw new ApplicationException("Unknown order state: " + order.OrderState);
                }
                if (order.LogicalOrderId == 0)
                {
                    if (order.Side == OrderSide.Buy)
                    {
                        pendingAdjustments += order.RemainingSize;
                    }
                    else
                    {
                        pendingAdjustments -= order.RemainingSize;
                    }
                    if (positionDelta > 0)
                    {
                        if (pendingAdjustments > positionDelta)
                        {
                            TryCancelBrokerOrder(order);
                            pendingAdjustments -= order.RemainingSize;
                        }
                        else if (pendingAdjustments < 0)
                        {
                            TryCancelBrokerOrder(order);
                            pendingAdjustments += order.RemainingSize;
                        }
                    }
                    if (positionDelta < 0)
                    {
                        if (pendingAdjustments < positionDelta)
                        {
                            TryCancelBrokerOrder(order);
                            pendingAdjustments += order.RemainingSize;
                        }
                        else if (pendingAdjustments > 0)
                        {
                            TryCancelBrokerOrder(order);
                            pendingAdjustments -= order.RemainingSize;
                        }
                    }
                    if (positionDelta == 0)
                    {
                        TryCancelBrokerOrder(order);
                        pendingAdjustments += order.Side == OrderSide.Sell ? order.RemainingSize : -order.RemainingSize;
                    }
                    physicalOrders.Remove(order);
                }
            }
			return pendingAdjustments;
		}

        public void TrySyncPosition(Iterable<StrategyPosition> strategyPositions)
        {
            physicalOrderCache.SyncPositions(strategyPositions);
            SyncPosition();
        }

	    private void SyncPosition()
        {
            if (symbol.DisableRealtimeSimulation)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG454, symbol.DisableRealtimeSimulation);
                isPositionSynced = true;
                return;
            }
            if (!ReceivedDesiredPosition)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG453, ReceivedDesiredPosition);
                return;
            }
            // Find any pending adjustments.
            var pendingAdjustments = FindPendingAdjustments();
            var positionDelta = desiredPosition - physicalOrderCache.GetActualPosition(symbol);
			var delta = positionDelta - pendingAdjustments;
			PhysicalOrderDefault physical;
            if( delta != 0)
            {
                isPositionSynced = false;
                log.Info("SyncPosition() Issuing adjustment order because expected position is " + desiredPosition + " but actual is " + physicalOrderCache.GetActualPosition(symbol) + " plus pending adjustments " + pendingAdjustments);
                if (debug) log.DebugFormat(LogMessage.LOGMSG455, tickSync.State);
            }
            else if( positionDelta == 0)
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG456, desiredPosition, physicalOrderCache.GetActualPosition(symbol), pendingAdjustments);
                isPositionSynced = true;
            }
			if( delta > 0)
			{
			    physical = physicalOrderCache.Create();
                physical.Initialize(OrderAction.Create, OrderState.Pending, symbol, OrderSide.Buy, OrderType.Market, OrderFlags.None, 0, (int) delta, 0, 0, 0, null, default(TimeStamp));
                log.Info("Sending adjustment order to position: " + physical);
			    TryCreateBrokerOrder(physical, null);
            }
            else if (delta < 0)
            {
                OrderSide side;
                var pendingDelta = physicalOrderCache.GetActualPosition(symbol) + pendingAdjustments;
                var sendAdjustment = false;
				if( pendingDelta > 0) {
					side = OrderSide.Sell;
				    delta = Math.Min(pendingDelta, -delta);
				    sendAdjustment = true;
				}
                else if (pendingAdjustments == 0)
                {
                    side = OrderSide.SellShort;
                    sendAdjustment = true;
                }
                if( sendAdjustment)
                {
                    side = physicalOrderCache.GetActualPosition(symbol) >= Math.Abs(delta) ? OrderSide.Sell : OrderSide.SellShort;
                    physical = physicalOrderCache.Create();
                    physical.Initialize(OrderAction.Create, OrderState.Pending, symbol, side, OrderType.Market, OrderFlags.None, 0, (int) Math.Abs(delta), 0, 0, 0, null, default(TimeStamp));
                    log.Info("Sending adjustment order to correct position: " + physical);
                    TryCreateBrokerOrder(physical, null);
                }
            }
        }

        public void SetStrategyPositions(Iterable<StrategyPosition> strategyPositions)
        {
            physicalOrderCache.SyncPositions(strategyPositions);
		}

        public void SetLogicalOrders(Iterable<LogicalOrder> inputLogicals)
        {
            if (trace)
            {
                int count = originalLogicals == null ? 0 : originalLogicals.Count;
                log.TraceFormat(LogMessage.LOGMSG457, count);
            }
            logicalOrderCache.SetActiveOrders(inputLogicals);
            bufferedLogicals.Clear();
            var list = logicalOrderCache.GetActiveOrders();
            for (var current = list.First; current != null; current = current.Next )
            {
                var active = current.Value;
                bufferedLogicals.Add(active);
            }
            bufferedLogicalsChanged = true;
            if (debug) log.DebugFormat(LogMessage.LOGMSG458, bufferedLogicals.Count);
        }
		
		public void SetDesiredPosition(	int position)
		{
		    receivedDesiredPosition = true;
			this.desiredPosition = position;
		}

		private bool CheckForPendingInternal() {
			var result = false;
		    for(var i=0; i< originalPhysicals.Count; i++)
		    {
		        var order = originalPhysicals[i];
				if( order.IsPending)
				{
					result = true;	
				}
            }
			return result;
		}

        private bool CheckForMarketOrdersInternal()
        {
            var result = false;
            for (var i = 0; i < originalPhysicals.Count; i++)
            {
                var order = originalPhysicals[i];
                if (order.Type == OrderType.Market)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG459, order);
                    result = true;
                }
            }
            return result;
        }

        public void ProcessHeartBeat()
        {
        }

        public bool CheckForPending()
        {
            //var expiryLimit = Factory.Parallel.UtcNow;
            //if( Factory.IsAutomatedTest)
            //{
            //    expiryLimit.AddMilliseconds(-10);
            //}
            //else
            //{
            //    expiryLimit.AddSeconds(-30);
            //}
            //if (trace) log.TraceFormat(LogMessage.LOGMSG460, expiryLimit);
            var foundAny = false;
            //var cancelList = physicalOrderCache.GetOrdersList((x) => x.Symbol == symbol && (x.IsPending) && x.Action == OrderAction.Cancel);
            //if( HandlePending(cancelList,expiryLimit))
            //{
            //    foundAny = true;
            //}
            //var orderList = physicalOrderCache.GetOrdersList((x) => x.Symbol == symbol && (x.IsPending) && x.Action != OrderAction.Cancel);
            //if( HandlePending(orderList,expiryLimit))
            //{
            //    foundAny = true;
            //}
            return foundAny;
        }

        private bool HandlePending( List<PhysicalOrder> list, TimeStamp expiryLimit) {
            var cancelOrders = new List<PhysicalOrder>();
            var foundAny = false;
            foreach( var order in list)
            {
                foundAny = true;
                if( debug) log.DebugFormat(LogMessage.LOGMSG461, order);
                var lastChange = order.LastModifyTime;
                if( order.ReplacedBy != null)
                {
                    lastChange = order.ReplacedBy.LastModifyTime;
                }
                if( lastChange < expiryLimit)
                {
                    if( order.Action == OrderAction.Cancel)
                    {
                        order.OrderState = OrderState.Expired;
                        if (debug) log.DebugFormat(LogMessage.LOGMSG462, order);
                        var origOrder = order.OriginalOrder;
                        if (origOrder != null)
                        {
                            origOrder.ReplacedBy = null;
                        }
                        cancelOrders.Add(order);
                    }
                    else
                    {
                        order.OrderState = OrderState.Expired;
                        var diff = Factory.Parallel.UtcNow - lastChange;
                        var message = "Attempting to cancel pending order " + order.BrokerOrder + " because it is stale over " + diff.TotalSeconds + " seconds.";
                        if (DoSyncTicks)
                        {
                            log.Info(message);
                        }
                        else
                        {
                            log.Warn(message);
                        }
                        if (!CancelStale(order))
                        {
                            if( debug) log.DebugFormat(LogMessage.LOGMSG463, order);
                        }
                    }
                }
            }
            if( cancelOrders.Count > 0)
            {
                PerformCompareProtected();
                foreach( var order in cancelOrders)
                {
                    physicalOrderCache.RemoveOrder(order.BrokerOrder);
                    if( order.OriginalOrder.OrderState != OrderState.Expired)
                    {
                        tickSync.RemovePhysicalOrder(order);
                    }
                }
            }
            return foundAny;
        }

        private LogicalOrder FindActiveLogicalOrder(long serialNumber)
        {
            for(var i=0; i<originalLogicals.Count; i++)
            {
                var order = originalLogicals[i];
                if (order.SerialNumber == serialNumber)
                {
                    return order;
                }
            }
            return null;
        }

        public void SyntheticFill(PhysicalFill synthetic)
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG464, synthetic);
            PhysicalOrder syntheticOrder;
            if (!physicalOrderCache.TryGetOrderById(synthetic.BrokerOrder, out syntheticOrder))
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG465, synthetic.BrokerOrder);
                TryRemovePhysicalFill(synthetic);
                return;
            }
            CleanupFilledPhysicalOrder(syntheticOrder);
            LogicalOrder logical = null;
            try
            {
                logical = logicalOrderCache.FindLogicalOrder(syntheticOrder.LogicalSerialNumber);
            }
            catch( ApplicationException)
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG466, syntheticOrder.LogicalSerialNumber);
                TryRemovePhysicalFill(synthetic);
                return;
            }
            if (DoSyncTicks)
            {
                if (!tickSync.SentWaitingMatch)
                {
                    tickSync.AddWaitingMatch("SyntheticFill");
                }
            }
            TryAddTouchedLogicalStop(symbol,logical, synthetic);
            if (debug) log.DebugFormat(LogMessage.LOGMSG467);
            PerformCompareProtected();
        }

        public void Clear()
        {
            syntheticOrderHandler.Clear();
        }

        public void ProcessFill(PhysicalFill physical)
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG468, physical);
		    PhysicalOrder order;
            if( !physicalOrderCache.TryGetOrderById(physical.BrokerOrder, out order))
		    {
		        throw new ApplicationException("Cannot find physical order for id " + physical.BrokerOrder + " in fill: " + physical);
		    }
		    var adjustment = order.LogicalOrderId == 0;
            var beforePosition = physicalOrderCache.GetActualPosition(symbol);
		    physicalOrderCache.IncreaseActualPosition(symbol, physical.Size);
            if (debug) log.DebugFormat(LogMessage.LOGMSG469, beforePosition, physicalOrderCache.GetActualPosition(symbol), physical.Size);
			var isCompletePhysicalFill = physical.RemainingSize == 0;
            TryFlushBufferedLogicals();

		    if( isCompletePhysicalFill) {
                CleanupFilledPhysicalOrder(order);
		    }
            else
            {
                order.CompleteSize = Math.Abs(physical.CompleteSize);
                order.CumulativeSize = Math.Abs(physical.CumulativeSize);
                order.RemainingSize = Math.Abs(physical.RemainingSize);
                if (debug) log.DebugFormat(LogMessage.LOGMSG470, order);
            }

            if( adjustment) {
                if (debug) log.DebugFormat(LogMessage.LOGMSG471, desiredPosition, order);
                if (debug) log.DebugFormat(LogMessage.LOGMSG472);
                if (debug) log.DebugFormat(LogMessage.LOGMSG473);
                isPositionSynced = false; // Force a check for position synchronized.
                PerformCompareProtected();
                TryRemovePhysicalFill(physical);
                return;
            }

            var isFilledAfterCancel = false;

            var logical = FindActiveLogicalOrder(order.LogicalSerialNumber);
            if (logical == null)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG474, physical);
                isFilledAfterCancel = true;
            }
            else
            {
                if (order.Type != OrderType.Market && logical.Price.ToLong() != order.Price.ToLong())
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG475, order.Price, logical);
                    if (debug) log.DebugFormat(LogMessage.LOGMSG476, order.OffsetTooLateToChange);
                    if (order.OffsetTooLateToChange)
                    {
                        isFilledAfterCancel = true;
                    }
                }
            }

            if (debug) log.DebugFormat(LogMessage.LOGMSG477, isFilledAfterCancel);
            if (isFilledAfterCancel)
            {
                TryRemovePhysicalFill(physical);
                if (debug) log.DebugFormat(LogMessage.LOGMSG478, order.OffsetTooLateToChange);
                    if (ReceivedDesiredPosition)
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG479, order.ReplacedBy);
                        SyncPosition();
                    }
                return;
            } 

            if( logical == null)
            {
                throw new InvalidOperationException("Logical cannot be null");
            }

		    LogicalFillBinary fill;
            desiredPosition += physical.Size;
            var strategyPosition = GetStrategyPosition(logical);
            if (debug) log.DebugFormat(LogMessage.LOGMSG480, desiredPosition, physical.Size);
            var position = strategyPosition + physical.Size;
            if (debug) log.DebugFormat(LogMessage.LOGMSG481, position, strategyPosition);
            if (position != strategyPosition)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG482, position, strategyPosition, logical);
            }
            ++recency;
            fill = new LogicalFillBinary(position, recency, physical.Price, physical.Time, physical.UtcTime, order.LogicalOrderId, order.LogicalSerialNumber, logical.Position, physical.IsExitStategy, physical.IsActual);
            if (debug) log.DebugFormat(LogMessage.LOGMSG483, fill);
            ProcessFill(fill, logical, isCompletePhysicalFill, physical.IsRealTime);
		}

        private void TryAddTouchedLogicalStop(SymbolInfo symbol, LogicalOrder logical, PhysicalFill synthetic)
        {
            if( logical.Type == OrderType.Stop && !logical.IsTouched)
            {
                logical.SetTouched(synthetic.UtcTime);
                if( OnProcessTouch != null)
                {
                    ++recency;
                    var touch = new LogicalTouchBinary(logical.Id, logical.SerialNumber, recency, synthetic.UtcTime);
                    OnProcessTouch(symbol, touch);
                }
                else
                {
                    throw new InvalidOperationException("OnProcessTouch was never set.");
                }
            }
            else
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG484, logical);
                TryRemovePhysicalFill(synthetic);
            }
		}

        private void CleanupFilledPhysicalOrder(PhysicalOrder order)
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG485, order);
            order.OrderState = OrderState.Filled;
            originalPhysicals.Remove(order);
            physicalOrders.Remove(order);
            // Leave it in the cache so a too late to cancel or too late to change order can find it.
            // physicalOrderCache.RemoveOrder(order.BrokerOrder);
            var replacedBy = order.ReplacedBy;
            if (replacedBy != null)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG486, replacedBy);
                CleanupFilledPhysicalOrder(replacedBy);
                if (DoSyncTicks)
                {
                    tickSync.RemovePhysicalOrder(order.ReplacedBy);
                }
            }
        }

        private TaskLock performCompareLocker = new TaskLock();
		private void PerformCompareProtected()
		{
		    var count = ++recursiveCounter;
		    var compareSuccess = false;
		    if( count == 1)
		    {
				while( recursiveCounter > 0)
				{
                    for (var i = 0; i < recursiveCounter-1; i++ )
                    {
                        --recursiveCounter;
                    }
					try
					{
                        if (!isPositionSynced)
                        {
                            SyncPosition();
                        }

                        CheckForPending();

                        // Is it still not synced?
                        if (isPositionSynced)
                        {
                            compareSuccess = PerformCompareInternal();
                            if( debug)
                            {
                                log.DebugFormat(LogMessage.LOGMSG487, compareSuccess);
                            }
                            if (trace) log.TraceFormat(LogMessage.LOGMSG488, tickSync.State);
                        }
                        else
                        {
                            var extra = DoSyncTicks ? tickSync.ToString() : "";
                            if (debug) log.DebugFormat(LogMessage.LOGMSG489, extra);
                        }

					}
                    finally
					{
					    --recursiveCounter;
					}
				}
            }
            else
			{
			    if( debug) log.DebugFormat(LogMessage.LOGMSG490, count, tickSync.State);
			}
            if (compareSuccess)
            {
                if (DoSyncTicks && !handleSimulatedExits)
                {
                    if (tickSync.SentWaitingMatch)
                    {
                        tickSync.RemoveWaitingMatch("PerformCompare");
                    }
                }
                if( rejectRepeatCounter > 0 && confirmedOrderCount > 0)
                {
                    if( debug) log.DebugFormat(LogMessage.LOGMSG491, confirmedOrderCount);
                    rejectRepeatCounter = 0;
                }
            }
            if (DoSyncTicks && !compareSuccess && isBrokerOnline)
            {
                if (!tickSync.SentWaitingMatch)
                {
                    tickSync.AddWaitingMatch("PositionChange");
                }
            }
		}
		
		private void TryRemovePhysicalFill(PhysicalFill fill) {
            if (DoSyncTicks) tickSync.RemovePhysicalFill(fill);
		}
		
		private void ProcessFill( LogicalFillBinary fill, LogicalOrder filledOrder, bool isCompletePhysicalFill, bool isRealTime) {
			if( debug) log.DebugFormat(LogMessage.LOGMSG492, fill + (!isRealTime ? " NOTE: This is NOT a real time fill." : ""));
			int orderId = fill.OrderId;
			if( orderId == 0) {
				// This is an adjust-to-position market order.
				// Position gets set via SetPosition instead.
				return;
			}

			if( debug) log.DebugFormat(LogMessage.LOGMSG493, filledOrder);

		    var strategyPosition = GetStrategyPosition(filledOrder);
            var orderPosition = filledOrder.Side == OrderSide.Buy ? filledOrder.Position : -filledOrder.Position;
            if (filledOrder.TradeDirection == TradeDirection.Change)
            {
				if( debug) log.DebugFormat(LogMessage.LOGMSG494, orderPosition, strategyPosition, fill.Position);
				fill.IsComplete = orderPosition + strategyPosition == fill.Position;
                var change = fill.Position - strategyPosition;
                filledOrder.Position = Math.Abs(orderPosition - change);
                if (debug) log.DebugFormat(LogMessage.LOGMSG495, filledOrder.Position);
            }
            else
            {
                fill.IsComplete = CheckFilledOrder(filledOrder, fill.Position);
            }
		    filledOrder.SetPartialFill(fill.UtcTime);
            if (fill.IsComplete)
			{
                if (debug) log.DebugFormat(LogMessage.LOGMSG496);
			    MarkAsFilled(filledOrder);
            }
            UpdateOrderCache(filledOrder, fill);
            CleanupAfterFill(filledOrder, fill);
            if (isCompletePhysicalFill && !fill.IsComplete)
            {
                if (filledOrder.TradeDirection == TradeDirection.Entry && fill.Position == 0)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG497, filledOrder);
                    MarkAsFilled(filledOrder);
                    CleanupAfterFill(filledOrder, fill);
                }
                else if( isRealTime)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG498);
                    //var matches = TryMatchId(physicalOrderCache.GetActiveOrders(symbol), filledOrder);
                    //if( matches.Count > 0)
                    //{
                    //    ProcessMatch(filledOrder, matches);
                    //}
                    //else
                    //{
                    //    ProcessMissingPhysical(filledOrder);
                    //}
                }
			}
            if (onProcessFill != null)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG499, symbol, fill);
                onProcessFill(symbol, fill);
            }
			if( debug) log.DebugFormat(LogMessage.LOGMSG473);
			PerformCompareProtected();
        }

        private void MarkAsFilled(LogicalOrder filledOrder)
        {
            try
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG500, filledOrder.Id);
                originalLogicals.Remove(filledOrder);
            }
            catch (ApplicationException ex)
            {
                log.Warn("Ignoring exception and continuing: " + ex.Message, ex);
            }
            catch (ArgumentException ex)
            {
                log.Error(ex.Message + " Was the order already marked as filled? : " + filledOrder);
            }
        }

        private void CancelLogical(LogicalOrder order)
        {
            if( debug) log.DebugFormat(LogMessage.LOGMSG501, order);
            originalLogicals.Remove(order);
        }

		private void CleanupAfterFill(LogicalOrder filledOrder, LogicalFillBinary fill) {
			bool clean = false;
			bool cancelAllEntries = false;
			bool cancelAllExits = false;
			bool cancelAllExitStrategies = false;
			bool cancelAllReverse = false;
			bool cancelAllChanges = false;
            if( fill.IsComplete)
            {
                var strategyPosition = GetStrategyPosition(filledOrder);
                if (strategyPosition == 0)
                {
                    cancelAllChanges = true;
                    clean = true;
                    if (debug) log.DebugFormat(LogMessage.LOGMSG502, strategyPosition);
                }
                switch (filledOrder.TradeDirection)
                {
                    case TradeDirection.Change:
                        break;
                    case TradeDirection.Entry:
                        cancelAllEntries = true;
                        clean = true;
                        if (debug) log.DebugFormat(LogMessage.LOGMSG503);
                        break;
                    case TradeDirection.Exit:
                    case TradeDirection.ExitStrategy:
                        cancelAllExits = true;
                        cancelAllExitStrategies = true;
                        cancelAllEntries = true;
                        cancelAllChanges = true;
                        clean = true;
                        if (debug) log.DebugFormat(LogMessage.LOGMSG504);
                        break;
                    case TradeDirection.Reverse:
                        cancelAllReverse = true;
                        cancelAllEntries = true;
                        clean = true;
                        if (debug) log.DebugFormat(LogMessage.LOGMSG505);
                        break;
                    default:
                        throw new ApplicationException("Unknown trade direction: " + filledOrder.TradeDirection);
                }
            }
            else
            {
                switch (filledOrder.TradeDirection)
                {
                    case TradeDirection.Change:
                    case TradeDirection.Entry:
                        break;
                    case TradeDirection.Exit:
                    case TradeDirection.ExitStrategy:
                    case TradeDirection.Reverse:
                        cancelAllEntries = true;
                        cancelAllChanges = true;
                        clean = true;
                        if (debug) log.DebugFormat(LogMessage.LOGMSG506);
                        break;
                    default:
                        throw new ApplicationException("Unknown trade direction: " + filledOrder.TradeDirection);
                }
            }
			if( clean) {
			    for(var i = 0; i<originalLogicals.Count; i++)
			    {
			        var order = originalLogicals[i];
					if( order.StrategyId == filledOrder.StrategyId) {
						switch( order.TradeDirection) {
							case TradeDirection.Entry:
								if( cancelAllEntries)
								{
								    CancelLogical(order);
								}
								break;
							case TradeDirection.Change:
                                if (cancelAllChanges)
                                {
                                    CancelLogical(order);
                                }
								break;
							case TradeDirection.Exit:
                                if (cancelAllExits)
                                {
                                    CancelLogical(order);
                                }
								break;
							case TradeDirection.ExitStrategy:
                                if (cancelAllExitStrategies)
                                {
                                    CancelLogical(order);
                                }
								break;
							case TradeDirection.Reverse:
                                if (cancelAllReverse)
                                {
                                    CancelLogical(order);
                                }
								break;
							default:
								throw new ApplicationException("Unknown trade direction: " + filledOrder.TradeDirection);
						}
					}
				}
			}
		}
	
		private void UpdateOrderCache(LogicalOrder order, LogicalFill fill)
		{
            var strategyPosition = GetStrategyPosition(order);
            if (debug) log.DebugFormat(LogMessage.LOGMSG507, strategyPosition, fill.Position, fill.Recency, order.StrategyId);
            if( handleSimulatedExits)
            {
                order.StrategyPosition = fill.Position;
            }
            else
            {
                physicalOrderCache.SetStrategyPosition(symbol, order.StrategyId, fill.Position);
            }
		}
		
		public int ProcessOrders() {
            if (debug) log.DebugFormat(LogMessage.LOGMSG508);
			PerformCompareProtected();
            return 0;
		}

		private int recursiveCounter;
		private bool PerformCompareInternal()
		{
		    var result = true;

            if (debug)
			{
                var mismatch = physicalOrderCache.GetActualPosition(symbol) == desiredPosition ? "match" : "MISMATCH";
			    log.DebugFormat(LogMessage.LOGMSG509, symbol, physicalOrderCache.GetActualPosition(symbol), desiredPosition, mismatch);
			}
				
            originalPhysicals.Clear();
		    physicalOrderCache.GetActiveOrders(tempActiveOrders, symbol);
            for (var i = 0; i < tempActiveOrders.Count; i++ )
            {
                originalPhysicals.Add(tempActiveOrders[i]);
            }

            TryFlushBufferedLogicals();

            if (debug)
            {
                log.DebugFormat(LogMessage.LOGMSG510, originalLogicals.Count, originalPhysicals.Count);
            }

            if (debug)
            {
                LogOrders(originalLogicals, "Original Logical");
                LogOrders(originalPhysicals, "Original Physical");
            }

            var hasPendingOrders = CheckForPendingInternal();
            if (hasPendingOrders)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG511);
                return false;
            }

            var hasMarketOrders = CheckForMarketOrdersInternal();
            if (hasMarketOrders)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG512);
            }

            logicalOrders.Clear();
            for (var i = 0; i < originalLogicals.Count; i++ )
            {
                logicalOrders.Add(originalLogicals[i]);
            }
			
			physicalOrders.Clear();
			if(originalPhysicals != null) {
                for (var i = 0; i < originalPhysicals.Count; i++ )
                {
                    physicalOrders.Add(originalPhysicals[i]);
                }
			}

			PhysicalOrder physical;
			extraLogicals.Clear();
			while( logicalOrders.Count > 0)
			{
				var logical = logicalOrders[0];
			    var matches = TryMatchId(physicalOrders, logical);
                if( matches.Count > 0)
                {
                    if( hasMarketOrders)
                    {
                        foreach( var order in matches)
                        {
                            physicalOrders.Remove(order);
                        }
                        result = false;
                    }
                    else if( !ProcessMatch( logical, matches))
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG513, logical);
                        result = false;
                    }
                }
                else
                {
                    extraLogicals.Add(logical);
				}
				logicalOrders.Remove(logical);
			}

			if( trace) log.TraceFormat(LogMessage.LOGMSG514, physicalOrders.Count);
			int cancelCount = 0;
            if( physicalOrders.Count > 0)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG515, physicalOrders.Count);
                result = false;
            }
			while( physicalOrders.Count > 0)
			{
			    physical = physicalOrders[0];
				if( TryCancelBrokerOrder(physical)) {
					cancelCount++;
				}
				physicalOrders.RemoveAt(0);
			}
			
			if( cancelCount > 0) {
				// Wait for cancels to complete before creating any orders.
				return result;
			}

            if (hasMarketOrders)
            {
                result = false;
            }
            else
		    {
                if (trace) log.TraceFormat(LogMessage.LOGMSG516, extraLogicals.Count);
                while (extraLogicals.Count > 0)
                {
                    var logical = extraLogicals[0];
                    if (!ProcessExtraLogical(logical))
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG517, logical);
                        result = false;
                    }
                    extraLogicals.Remove(logical);
                }
            }
            return result;
        }

        private void TryFlushBufferedLogicals()
        {
            if (bufferedLogicalsChanged)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG518);
                originalLogicals.Clear();
                if (bufferedLogicals != null)
                {
                    for (var i = 0; i < bufferedLogicals.Count; i++ )
                    {
                        originalLogicals.Add(bufferedLogicals[i]);
                    }
                }
                bufferedLogicalsChanged = false;
            }
        }

        private void LogOrders( IEnumerable<LogicalOrder> orders, string name)
        {
            foreach(var order in orders)
            {
                log.DebugFormat(LogMessage.LOGMSG519, order);
            }
        }

        private void LogOrders( IEnumerable<PhysicalOrder> orders, string name)
        {
            if( debug)
            {
                var first = true;
                foreach (var order in orders)
                {
                    if( first)
                    {
                        log.DebugFormat(LogMessage.LOGMSG520, name);
                        first = false;
                    }
                    log.DebugFormat(LogMessage.LOGMSG33, name, order);
                }
                if( first)
                {
                    log.DebugFormat(LogMessage.LOGMSG521, name);
                }
            }
        }
	
		public long ActualPosition {
            get { return physicalOrderCache.GetActualPosition(symbol); }
		}

		public void SetActualPosition( long position)
		{
		    physicalOrderCache.SetActualPosition(symbol, position);
		}

        public void IncreaseActualPosition( int position)
        {
            var result = physicalOrderCache.IncreaseActualPosition(symbol, position);
            if( debug) log.DebugFormat(LogMessage.LOGMSG522, result);
        }

		public PhysicalOrderHandler PhysicalOrderHandler {
			get { return physicalOrderHandler; }
		}
		
		public Action<SymbolInfo,LogicalFillBinary> OnProcessFill {
			get { return onProcessFill; }
			set { onProcessFill = value; }
		}
		
		public bool HandleSimulatedExits {
			get { return handleSimulatedExits; }
			set { handleSimulatedExits = value; }
		}

	    public LogicalOrderCache LogicalOrderCache
	    {
	        get { return logicalOrderCache; }
	    }

        public bool IsSynchronized
        {
            get { return isPositionSynced; }
        }

	    public bool IsPositionSynced
	    {
	        get { return isPositionSynced; }
	        set { isPositionSynced = value; }
	    }

        public bool EnableSyncTicks
        {
            get { return enableSyncTicks; }
            set { enableSyncTicks = value; }
        }

        public int RejectRepeatCounter
        {
            get { return rejectRepeatCounter; }
            set { rejectRepeatCounter = value; }
        }

        public bool IsBrokerOnline
        {
            get { return isBrokerOnline; }
            set { isBrokerOnline = value; }
                    }

        public bool ReceivedDesiredPosition
        {
            get { return receivedDesiredPosition; }
        }

        public Action<SymbolInfo, LogicalTouch> OnProcessTouch
        {
            get { return onProcessTouch; }
            set { onProcessTouch = value; }
        }

        public SymbolInfo Symbol
        {
            get { return symbol; }
        }

        public bool DisableChangeOrders
        {
            get { return disableChangeOrders; }
            set { disableChangeOrders = value; }
        }

        // This is a callback to confirm order was properly placed.
        public void ConfirmChange(long brokerOrderId, long originalOrderId, Origin origin, bool isRealTime)
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG523, (isRealTime ? "RealTime" : "Recovery"), brokerOrderId);
            PhysicalOrder originalOrder = null;
            if( originalOrderId != 0)
            {
                if (physicalOrderCache.TryGetOrderById(originalOrderId, out originalOrder))
                {
                    physicalOrderCache.RemoveOrder(originalOrderId);
                }
                else
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG524, originalOrderId);
                }
            }

            PhysicalOrder order;
            if (physicalOrderCache.TryGetOrderById(brokerOrderId, out order))
            {
                if( order.OrderState == OrderState.Filled)
                {
                    if (debug) log.DebugFormat("Removing order because already filled: {0}", order);
                    physicalOrderCache.RemoveOrder(originalOrderId);
                }
                else if (order.OrderState != OrderState.Lost)
                {
                    ++confirmedOrderCount;
                    order.OrderState = OrderState.Active;
                    order.RemainingSize = order.CompleteSize - order.CumulativeSize;
                    order.OriginalOrder = null;
                    if (debug) log.DebugFormat(LogMessage.LOGMSG525, order);
                }
            }
            else
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG524, brokerOrderId);
                TryCreateLostOrder(brokerOrderId, origin);
            }

            if( originalOrder != null && order != null)
            {
                order.CumulativeSize = originalOrder.CumulativeSize;
            }

            if (isRealTime)
            {
                PerformCompareProtected();
            }
            if (DoSyncTicks)
            {
                tickSync.RemovePhysicalOrder(order);
            }
        }

        public bool HasBrokerOrder( PhysicalOrder order)
        {
            return false;
        }

        public void ConfirmActive(long brokerOrder, Origin origin, bool isRealTime)
        {
            PhysicalOrder order;
            if (physicalOrderCache.TryGetOrderById(brokerOrder, out order))
            {
                if( order.OrderState != OrderState.Lost)
                {
                    order.OrderState = OrderState.Active;
                    if (debug) log.DebugFormat(LogMessage.LOGMSG526, (isRealTime ? "RealTime" : "Recovery"), order);
                }
            }
            else
            {
                log.Warn("ConfirmActive: Cannot find physical order for id " + brokerOrder);
                TryCreateLostOrder(brokerOrder, origin);
            }
            if (isRealTime)
            {
                PerformCompareProtected();
            }
            if (DoSyncTicks)
            {
                tickSync.RemovePhysicalOrder(order);
            }
        }

        private bool TryCreateLostOrder(long orderId, Origin origin)
        {
            var physical = physicalOrderCache.Create();
            physical.Initialize(OrderState.Lost, symbol, orderId);
            physical.IsSynthetic = origin == Origin.Synthetic;
            if (debug) log.DebugFormat(LogMessage.LOGMSG434, physical);
            TryAddPhysicalOrder(physical);
            physicalOrderCache.SetOrder(physical);
            return true;
        }

        public void ConfirmCreate(long brokerOrder, Origin origin, bool isRealTime)
        {
            PhysicalOrder order;
            if (physicalOrderCache.TryGetOrderById(brokerOrder, out order))
            {
                ++confirmedOrderCount;
                if( order.OrderState != OrderState.Lost)
                {
                    order.OrderState = OrderState.Active;
                    if (debug) log.DebugFormat(LogMessage.LOGMSG527, (isRealTime ? "RealTime" : "Recovery"), order);
                }
            }
            else
            {
                log.Warn("ConfirmCreate: Cannot find physical order for id " + brokerOrder);
                TryCreateLostOrder(brokerOrder, origin);
            }
            if (isRealTime)
            {
                PerformCompareProtected();
            }
            if (DoSyncTicks)
            {
                tickSync.RemovePhysicalOrder(order);
            }
        }

        public void RejectOrder(long brokerOrder, bool isRealTime, bool retryImmediately)
        {
            PhysicalOrder order;
            if (!physicalOrderCache.TryGetOrderById(brokerOrder, out order))
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG528, brokerOrder);
                return;
            }
            ++rejectRepeatCounter;
            confirmedOrderCount = 0;
            if (debug) log.DebugFormat(LogMessage.LOGMSG529, RejectRepeatCounter, (isRealTime ? "RealTime" : "Recovery"), order);
            physicalOrderCache.RemoveOrder(order.BrokerOrder);
            var origOrder = order.OriginalOrder;
            if (origOrder != null)
            {
                if( origOrder.OrderState == OrderState.Expired)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG530, order.OriginalOrder);
                    physicalOrderCache.PurgeOriginalOrder(order);
                }
            }
            if (isRealTime && retryImmediately)
            {
                if (!CheckForPending())
                {
                    PerformCompareProtected();
                }
            }
            if (DoSyncTicks)
            {
                tickSync.RemovePhysicalOrder(order);
            }
        }

        public void ConfirmCancel(long originalOrderId, bool isRealTime)
        {
            PhysicalOrder origOrder;
            if (!physicalOrderCache.TryGetOrderById(originalOrderId, out origOrder))
            {
                log.DebugFormat(LogMessage.LOGMSG531, originalOrderId);
                return;
            }
            var cancelOrder = origOrder.ReplacedBy;
            ++confirmedOrderCount;
            if (debug) log.DebugFormat(LogMessage.LOGMSG532, (isRealTime ? "RealTime" : "Recovery"), originalOrderId);
            if( cancelOrder != null)
            {
                physicalOrderCache.RemoveOrder(cancelOrder.BrokerOrder);
            }
            physicalOrderCache.RemoveOrder(origOrder.BrokerOrder);
            if (isRealTime)
            {
			    PerformCompareProtected();
            }
            if (DoSyncTicks)
            {
                tickSync.RemovePhysicalOrder(origOrder);
            }
        }
		
		public Iterable<PhysicalOrder> GetActiveOrders(SymbolInfo symbol)
		{
			throw new NotImplementedException();
		}

    }
}
