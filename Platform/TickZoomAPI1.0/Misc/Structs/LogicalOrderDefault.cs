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

namespace TickZoom.Api
{
    /// <summary>
    /// Description of OrderCommon.
    /// </summary>
    [SerializeContract]
    public class LogicalOrderDefault : LogicalOrder
    {
        private static readonly Log log = Factory.SysLog.GetLogger("LogicalOrder");
        private readonly bool debug = log.IsDebugEnabled;
        private readonly bool trace = log.IsTraceEnabled;
        [SerializeMember(1)]
        internal LogicalOrderBinary binary = new LogicalOrderBinary();

        public LogicalOrderDefault()
        {
            
        }

        public LogicalOrderDefault(SymbolInfo symbol)
        {
            Initialize(symbol, 0, 0);
        }

        public LogicalOrderDefault(SymbolInfo symbol, StrategyInterface strategy)
        {
            if (strategy == null)
            {
                throw new ApplicationException("Strategy argument is null. Please use a different constructor.");
            }
            else
            {
                binary.strategy = strategy;
                var serial = strategy.Context.IncrementOrderSerialNumber(symbol.BinaryIdentifier);
                Initialize(symbol, strategy.Context.IncrementOrderId(), serial);
            }
        }

        public void SetMultiLevels(int size, int levels, int increment)
        {
            if (levels <= 0)
            {
                throw new InvalidOperationException("Levels were " + levels + ". Please set levels to greater than or equal to zero.");
            }
            if (increment <= 0)
            {
                throw new InvalidOperationException("Please set increment to greater than zero.");
            }
            this.Position = size * levels;
            this.LevelSize = size;
            this.Levels = levels;
            this.LevelIncrement = increment;
        }

        public LogicalOrderDefault(SymbolInfo symbol, int orderId)
        {
            Initialize(symbol, orderId, 0L);
        }

        public void Initialize(SymbolInfo symbol, int orderId, long serialNumber)
        {
            binary.id = orderId;
            binary.symbol = symbol;
            binary.minimumTick = symbol.MinimumTick;
            binary.price = 0;
            binary.status = OrderStatus.Inactive;
            binary.position = 0;
            binary.type = OrderType.Market;
            binary.side = OrderSide.Sell;
            binary.tradeDirection = TradeDirection.Entry;
            binary.serialNumber = serialNumber;
            binary.tag = null;
            binary.levels = 1;
            binary.levelIncrement = 0;
            if (symbol.OffsetTooLateToChange)
            {
                binary.orderFlags |= OrderFlags.OffsetTooLateToChange;
            }
        }

        public double Price
        {
            get { return binary.price; }
            set
            {
                var orig = binary.price;
                binary.price = value;
                AdjustPrice();
                if (binary.price != orig)
                {
                    Modified();
                }
            }
        }

        private void Modified()
        {
            binary.isModified = true;
            if (binary.IsActive)
            {
                if (binary.strategy != null)
                {
                    binary.strategy.OrderModified(this);
                }
                if (binary.onModified != null)
                {
                    binary.onModified(this);
                }
            }
        }

        public OrderType Type
        {
            get { return binary.type; }
            set
            {
                if (binary.type != value)
                {
                    binary.type = value;
                    Modified();
                    if (binary.isInitialized)
                    {
                        throw new ApplicationException("Unable to change logical order Type after first use.");
                    }
                }
            }
        }

        public int Position
        {
            get { return binary.position; }
            set
            {
                if (binary.position != value)
                {
                    if (trace) log.TraceFormat(LogMessage.LOGMSG654, binary.position, value);
                    binary.position = value;
                    if (binary.tradeDirection == TradeDirection.Exit || binary.tradeDirection == TradeDirection.ExitStrategy)
                    {
                        if (binary.position != 0)
                        {
                            throw new ApplicationException("Position must be zero for Exit orders.");
                        }
                    }
                    Modified();
                }
            }
        }

        internal void AdjustPrice()  //(OrderCommon order)
        {
            // Orders that are based on indicator values can have prices that are not on an even tick boundary.  Since the
            // orders are fed to the tick factory, their prices have to be adjusted so they are on an even tick boundary for
            // the given market.  The adjustment is made in the direction of market movement needed to fill the order.

            // This gets the number of ticks in the order priced, then multiplies by
            // the size of a tick, and can get a wrong answer.
            // If roundedPrice is 0.3354 then evenPrice is reported as 0.3353.
            // If roundedPrice is 0.3333 then evenPrice is reported as 0.3332.  Both these are corrected by adding another round, with 0.
            if (double.IsNaN(binary.minimumTick) || binary.minimumTick == 0)
            {
                throw new ApplicationException("Please set the MinimumTick property in your ModelLoader via ProjectProperties.Start.SymbolProperties. " +
                                               "MinimumTick must be set in order to round order prices to the nearest minimum tick size.");
            }
            binary.price = binary.price.Round();
            double numberOfTicks = Math.Round(binary.price / binary.minimumTick, 0);
            double evenPrice = (numberOfTicks * binary.minimumTick).Round();

            var buyStop = binary.side == OrderSide.Buy && binary.type == OrderType.Stop;
            var sellStop = binary.side != OrderSide.Buy && binary.type == OrderType.Stop;
            var buyLimit = binary.side == OrderSide.Buy && binary.type == OrderType.Limit;
            var sellLimit = binary.side != OrderSide.Buy && binary.type == OrderType.Limit;

            if (evenPrice < binary.price && (buyStop || sellLimit))
            {
                binary.price = evenPrice + binary.minimumTick;
            }
            else if (evenPrice > binary.price && (sellStop || buyLimit))
            {
                binary.price = evenPrice - binary.minimumTick;
            }
            else
            {
                binary.price = evenPrice;
            }
            binary.price = binary.price.Round();
        }

        public TradeDirection TradeDirection
        {
            get { return binary.tradeDirection; }
            set
            {
                if (binary.tradeDirection != value)
                {
                    binary.tradeDirection = value;
                    if (binary.isInitialized)
                    {
                        throw new ApplicationException("Unable to change logical order TradeDirection after first use.");
                    }
                }
            }
        }


        public int CompareTo(object obj)
        {
            LogicalOrderDefault other = obj as LogicalOrderDefault;
            if (other == null)
            {
                return -1;
            }
            return binary.type.CompareTo(other.binary.type);
        }

        public string Tag
        {
            get { return binary.tag; }
            set { binary.tag = value; }
        }

        public void RefreshSerialNumber()
        {
            if (binary.strategy != null)
            {
                binary.serialNumber = binary.strategy.Context.IncrementOrderSerialNumber(binary.symbol.BinaryIdentifier);
            }
        }

        public bool IsAutoCancel
        {
            get { return binary.status == OrderStatus.AutoCancel; }
        }

        public OrderStatus Status
        {
            get { return binary.status; }
            set
            {
                if (binary.status != value)
                {
                    switch (value)
                    {
                        case OrderStatus.Inactive:
                            RefreshSerialNumber();
                            break;
                        case OrderStatus.AutoCancel:
                            if (binary.status == OrderStatus.Active || binary.status == OrderStatus.NextBar)
                            {
                                binary.isModified = false;
                                binary.status = value;
                            }
                            return;
                        case OrderStatus.NextBar:
                            if (binary.IsActive)
                            {
                                return;
                            }
                            if (binary.status == OrderStatus.AutoCancel)
                            {
                                value = OrderStatus.Active;
                            }
                            break;
                        case OrderStatus.Touched:
                        case OrderStatus.PartialFill:
                            if (binary.IsActive)
                            {
                                binary.status = value;
                            }
                            return;
                        case OrderStatus.Active:
                            if (binary.status == OrderStatus.AutoCancel)
                            {
                                // These were simply re-activated. So don't mark them changed until
                                // some other property was modified.
                                if (!binary.isModified)
                                {
                                    binary.status = value;
                                    return;
                                }
                            }
                            break;
                        default:
                            throw new ApplicationException("Unknown order status: " + value);
                    }
                    binary.status = value;
                    if (binary.strategy != null)
                    {
                        binary.strategy.OrderModified(this);
                    }
                    if (binary.onModified != null)
                    {
                        binary.onModified(this);
                    }
                }
            }
        }

        public override bool Equals(object obj)
        {
            return obj is LogicalOrderDefault && Equals((LogicalOrderDefault)obj);
        }

        public override int GetHashCode()
        {
            return binary.id;
        }

        private bool Equals(LogicalOrderDefault other)
        {
            return binary.id == other.binary.id;
        }

        public int Id
        {
            get { return binary.id; }
        }

        public object Strategy
        {
            get { return binary.strategy; }
        }

        public Action<LogicalOrder> OnModified
        {
            get { return binary.onModified; }
            set { binary.onModified = value; }
        }

        public int StrategyPosition
        {
            get { return binary.strategyPosition; }
            set { binary.strategyPosition = value; }
        }

        public int StrategyId
        {
            get
            {
                return binary.strategy == null ? binary.strategyId : binary.strategy.Id;
            }
            set
            {
                binary.strategyId = value;
            }
        }

        public long SerialNumber
        {
            get { return binary.serialNumber; }
        }

        public bool IsInitialized
        {
            get { return binary.isInitialized; }
            set { binary.isInitialized = value; }
        }
        private ActiveListNode<LogicalOrder> node;

        public ActiveListNode<LogicalOrder> Node
        {
            get { return node; }
            set { node = value; }
        }
        private ActiveListNode<LogicalOrder> changeNode;

        public ActiveListNode<LogicalOrder> ChangeNode
        {
            get { return changeNode; }
            set { changeNode = value; }
        }

        public TimeStamp UtcChangeTime
        {
            get { return binary.utcChangeTime; }
            set { binary.utcChangeTime = value; }
        }

        public int Levels
        {
            get { return binary.levels; }
            set
            {
                if (binary.levels != value)
                {
                    binary.levels = value;
                    Modified();
                }
            }
        }

        public int LevelIncrement
        {
            get { return binary.levelIncrement; }
            set
            {
                if (binary.levelIncrement != value)
                {
                    binary.levelIncrement = value;
                    Modified();
                }
            }
        }

        public int LevelSize
        {
            get { return binary.levelSize; }
            set
            {
                if (binary.levelSize != value)
                {
                    binary.levelSize = value;
                    Modified();
                }
            }
        }

        public OrderFlags OrderFlags
        {
            get { return binary.orderFlags; }
            set
            {
                if (binary.orderFlags != value)
                {
                    binary.orderFlags = value;
                    Modified();
                }
            }
        }

        public OrderSide Side
        {
            get { return binary.side; }
            set
            {
                if (binary.side != value)
                {
                    binary.side = value;
                    Modified();
                    if (binary.isInitialized)
                    {
                        throw new ApplicationException("Unable to change logical order Side after first use.");
                    }
                }
            }
        }

        public bool OffsetTooLateToChange
        {
            get { return (binary.orderFlags & OrderFlags.OffsetTooLateToChange) > 0; }
            set
            {
                if (value)
                {
                    binary.orderFlags |= OrderFlags.OffsetTooLateToChange;
                }
                else
                {
                    binary.orderFlags &= ~OrderFlags.OffsetTooLateToChange;
                }
            }
        }

        public TimeStamp UtcTouchTime
        {
            get { return binary.utcTouchTime; }
        }

        public void SetTouched(TimeStamp utcTime)
        {
            if (!binary.IsTouched)
            {
                binary.utcTouchTime = utcTime;
            }
            Status = OrderStatus.Touched;
        }

        public bool IsActive
        {
            get { return binary.IsActive; }
        }

        public bool IsTouched
        {
            get { return binary.IsTouched; }
        }

        public bool IsNextBar
        {
            get { return binary.IsNextBar; }
        }

        public bool IsSynthetic
        {
            get { return binary.IsSynthetic; }
            set { binary.IsSynthetic = value; }
        }

        public void SetPartialFill(TimeStamp utcTime)
        {
            if (!binary.IsTouched)
            {
                binary.utcTouchTime = utcTime;
            }
            Status = OrderStatus.PartialFill;
        }

        public override string ToString()
        {
            return binary.ToString();
        }

        public object ToLog()
        {
            return binary;
        }

        #region Serializable Members


        public void ResetCompression()
        {
            // Not implemented
        }

        #endregion
    }
}