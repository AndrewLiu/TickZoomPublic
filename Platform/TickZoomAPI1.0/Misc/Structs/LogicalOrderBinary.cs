using System;
using System.Text;
using ProtoBuf;

namespace TickZoom.Api
{
    [SerializeContract]
    public class LogicalOrderBinary
    {
        [SerializeMember(1)]
        public SymbolInfo symbol;

        [SerializeMember(2)]
        public double minimumTick;

        [SerializeMember(3)]
        public double price;

        [SerializeMember(4)]
        public OrderStatus status;

        [SerializeMember(5)]
        public int position;

        [SerializeMember(6)]
        public OrderType type;

        [SerializeMember(7)]
        public OrderSide side;

        [SerializeMember(8)]
        public TradeDirection tradeDirection;

        [SerializeMember(9)]
        public string tag;

        [SerializeMember(10)]
        public int id;

        [SerializeMember(11)]
        public long serialNumber;

        public StrategyInterface strategy;

        [SerializeMember(13)]
        public int strategyId;

        [SerializeMember(14)]
        public int strategyPosition;

        public Action<LogicalOrder> onModified;

        [SerializeMember(16)]
        public bool isInitialized = false;

        [SerializeMember(17)]
        public TimeStamp utcChangeTime = TimeStamp.UtcNow;

        [SerializeMember(18)]
        public TimeStamp utcTouchTime = default(TimeStamp);

        [SerializeMember(19)]
        public bool isModified = false;

        [SerializeMember(20)]
        public int levels = 1;

        [SerializeMember(21)]
        public int levelSize = 1;

        [SerializeMember(22)]
        public int levelIncrement = 0;

        [SerializeMember(23)]
        public OrderFlags orderFlags;

        public bool IsActive
        {
            get { return status == OrderStatus.Active || status == OrderStatus.PartialFill || status == OrderStatus.Touched; }
        }

        public bool IsTouched
        {
            get { return status == OrderStatus.Touched || status == OrderStatus.PartialFill; }
        }

        public bool IsNextBar
        {
            get { return status == OrderStatus.NextBar; }
        }

        public bool IsSynthetic
        {
            get { return (orderFlags & OrderFlags.IsSynthetic) > 0; }
            set
            {
                if (value)
                {
                    orderFlags |= OrderFlags.IsSynthetic;
                }
                else
                {
                    orderFlags &= ~OrderFlags.IsSynthetic;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if (IsSynthetic)
            {
                sb.Append("Synthetic ");
            }
            if (strategy != null)
            {
                sb.Append(strategy.Name);
                sb.Append(" - ");
            }
            sb.Append(symbol.ExpandedSymbol);
            sb.Append(" ( C");
            if (strategy != null)
            {
                sb.Append(strategy.Position.Current);
                sb.Append(", R");
                sb.Append(strategy.Result.Position.Current);
            }
            else
            {
                sb.Append(strategyPosition);
            }
            sb.Append(") Id:");
            sb.Append(id);
            sb.Append("-");
            sb.Append(serialNumber);
            sb.Append(" ");
            sb.Append(tradeDirection);
            sb.Append(" ");
            sb.Append(side);
            if (IsActive || IsNextBar)
            {
                if (position > 0)
                {
                    sb.Append(" ");
                    sb.Append(position);
                }
            }
            sb.Append(" ");
            sb.Append(type);
            if (IsActive || IsNextBar)
            {
                sb.Append(" at ");
                sb.Append(price);
            }
            if (IsActive || IsNextBar)
            {
                if (position > 0)
                {
                    sb.Append(" ");
                    sb.Append(position);
                }
                sb.Append(" at ");
                sb.Append(price);
            }
            sb.Append(" " + status);
            if (tag != null)
            {
                sb.Append(" -- ");
                sb.Append(tag);
            }
            else
            {
                sb.Append(" -- ");
                if (strategy != null)
                {
                    sb.Append(strategy.Name);
                    sb.Append(" #" + strategy.Id);
                }
                else
                {
                    sb.Append(" strategy #" + strategyId);
                }
            }
            if (levels > 1)
            {
                sb.Append(" " + levels + " levels at " + levelIncrement + " ticks apart.");
            }
            sb.Append(" change " + utcChangeTime);
            if (utcTouchTime != default(TimeStamp))
            {
                sb.Append(" touch " + utcTouchTime);
            }
            return sb.ToString();
        }
    }
}