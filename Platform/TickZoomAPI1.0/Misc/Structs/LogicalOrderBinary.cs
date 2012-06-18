using System;
using System.Text;

namespace TickZoom.Api
{
    public class LogicalOrderBinary
    {
        public SymbolInfo symbol;
        public double minimumTick;
        public double price;
        public OrderStatus status;
        public int position;
        public OrderType type;
        public OrderSide side;
        public TradeDirection tradeDirection;
        public string tag;
        public int id;
        public long serialNumber;
        public StrategyInterface strategy;
        public int strategyId;
        public int strategyPosition;
        public Action<LogicalOrder> onModified;
        public bool isInitialized = false;
        public TimeStamp utcChangeTime = TimeStamp.UtcNow;
        public TimeStamp utcTouchTime = default(TimeStamp);
        public bool isModified = false;
        public int levels = 1;
        public int levelSize = 1;
        public int levelIncrement = 0;
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