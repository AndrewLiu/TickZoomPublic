#region Copyright
/*
 * Copyright 2008 M. Wayne Walter
 * Software: TickZoom Trading Platform
 * User: Wayne Walter
 * 
 * You can use and modify this software under the terms of the
 * TickZOOM General Public License Version 1.0 or (at your option)
 * any later version.
 * 
 * Businesses are restricted to 30 days of use.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * TickZOOM General Public License for more details.
 *
 * You should have received a copy of the TickZOOM General Public
 * License along with this program.  If not, see
 * 
 * 
 *
 * User: Wayne Walter
 * Date: 5/18/2009
 * Time: 12:12 PM
 * <http://www.tickzoom.org/wiki/Licenses>.
 */
#endregion

using System;
using System.IO;

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
        private LogicalOrderBinary binary = new LogicalOrderBinary();

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

        private void Initialize(SymbolInfo symbol, int orderId, long serialNumber)
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


        unsafe public int FromReader(MemoryStream reader)
        {
            fixed (byte* fptr = reader.GetBuffer())
            {
                byte* sptr = fptr + reader.Position;
                byte* ptr = sptr;
                binary.id = *(int*)ptr; ptr += sizeof(int);
                long binaryId = *(long*)(ptr); ptr += sizeof(long);
                binary.symbol = Factory.Symbol.LookupSymbol(binaryId);
                binary.price = *(double*)(ptr); ptr += sizeof(double);
                binary.position = *(int*)(ptr); ptr += sizeof(int);
                binary.side = (OrderSide)(*(byte*)(ptr)); ptr += sizeof(byte);
                binary.type = (OrderType)(*(byte*)(ptr)); ptr += sizeof(byte);
                binary.tradeDirection = (TradeDirection)(*(byte*)(ptr)); ptr += sizeof(byte);
                binary.status = (OrderStatus)(*(byte*)(ptr)); ptr += sizeof(byte);
                binary.strategyId = *(int*)ptr; ptr += sizeof(int);
                binary.strategyPosition = *(int*)(ptr); ptr += sizeof(int);
                binary.serialNumber = *(long*)ptr; ptr += sizeof(long);
                binary.utcChangeTime.Internal = *(long*)ptr; ptr += sizeof(long);
                binary.utcTouchTime.Internal = *(long*)ptr; ptr += sizeof(long);
                binary.levels = *(int*)ptr; ptr += sizeof(int);
                binary.levelSize = *(int*)ptr; ptr += sizeof(int);
                binary.levelIncrement = *(int*)ptr; ptr += sizeof(int);
                binary.orderFlags = (OrderFlags)(*(int*)ptr); ptr += sizeof(int);
                reader.Position += (int)(ptr - sptr);
            }
            return 1;
        }

        private const int minOrderSize = 128;
        unsafe public void ToWriter(MemoryStream writer)
        {
            writer.SetLength(writer.Position + minOrderSize);
            byte[] buffer = writer.GetBuffer();
            fixed (byte* fptr = &buffer[writer.Position])
            {
                byte* ptr = fptr;
                *(int*)(ptr) = binary.id; ptr += sizeof(int);
                *(long*)(ptr) = binary.symbol.BinaryIdentifier; ptr += sizeof(long);
                *(double*)(ptr) = binary.price; ptr += sizeof(double);
                *(int*)(ptr) = binary.position; ptr += sizeof(int);
                *(byte*)(ptr) = (byte)binary.side; ptr += sizeof(byte);
                *(byte*)(ptr) = (byte)binary.type; ptr += sizeof(byte);
                *(byte*)(ptr) = (byte)binary.tradeDirection; ptr += sizeof(byte);
                *(byte*)(ptr) = Convert.ToByte(binary.status); ptr += sizeof(byte);
                if (binary.strategy != null)
                {
                    binary.strategyId = binary.strategy.Id;
                }
                *(int*)(ptr) = binary.strategyId; ptr += sizeof(int);
                *(int*)(ptr) = binary.strategyPosition; ptr += sizeof(int);
                *(long*)(ptr) = binary.serialNumber; ptr += sizeof(long);
                *(long*)(ptr) = binary.utcChangeTime.Internal; ptr += sizeof(long);
                *(long*)(ptr) = UtcTouchTime.Internal; ptr += sizeof(long);
                *(int*)(ptr) = binary.levels; ptr += sizeof(int);
                *(int*)(ptr) = binary.levelSize; ptr += sizeof(int);
                *(int*)(ptr) = binary.levelIncrement; ptr += sizeof(int);
                *(int*)(ptr) = (int)binary.orderFlags; ptr += sizeof(int);
                writer.Position += ptr - fptr;
                writer.SetLength(writer.Position);
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