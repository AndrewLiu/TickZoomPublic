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
using System.Text;
using System.Threading;

using TickZoom.Api;

namespace TickZoom.Common
{
    public struct PhysicalOrderBinary
    {
        public int sequence;
    	public OrderAction action;
        public OrderState orderState;
        public TimeStamp lastModifyTime;
        public TimeStamp lastReadTime;
        public SymbolInfo symbol;
        public OrderType type;
        public double price;
        public int completeSize;
        public int cumulativeSize;
        public int remainingSize;
        public OrderSide side;
        public int logicalOrderId;
        public long logicalSerialNumber;
        public long brokerOrder;
        public string tag;
        public object reference;
        public CreateOrChangeOrder originalOrder;
        public CreateOrChangeOrder replacedBy;
        public TimeStamp utcCreateTime;
        public OrderFlags orderFlags;
        public int cancelCount;
        public int pendingCount;
    }

	public class CreateOrChangeOrderDefault : CreateOrChangeOrder
	{
	    private static readonly Log log = Factory.SysLog.GetLogger(typeof (CreateOrChangeOrderDefault));
	    private static readonly bool debug = log.IsDebugEnabled;
        private PhysicalOrderBinary binary;
	    private long instanceId;
	    private static long nextInstanceId;
		
        public CreateOrChangeOrderDefault(OrderState orderState, SymbolInfo symbol, CreateOrChangeOrder origOrder)
        {
            binary.action = OrderAction.Cancel;
            OrderState = orderState;
            binary.lastModifyTime = Factory.Parallel.UtcNow;
            binary.symbol = symbol;
            binary.side = default(OrderSide);
            binary.type = default(OrderType);
            binary.price = 0D;
            binary.remainingSize = 0;
            binary.completeSize = 0;
            binary.cumulativeSize = 0;
            binary.logicalOrderId = 0;
            binary.logicalSerialNumber = 0L;
            binary.tag = null;
            binary.reference = null;
            binary.brokerOrder = CreateBrokerOrderId();
            binary.utcCreateTime = Factory.Parallel.UtcNow;
            if( origOrder == null)
            {
                throw new NullReferenceException("original order cannot be null for a cancel order.");
            }
            binary.originalOrder = origOrder;
            binary.replacedBy = null;
            binary.orderFlags = origOrder.OrderFlags;
            instanceId = ++nextInstanceId;
        }

        public CreateOrChangeOrder Clone()
        {
            var clone = new CreateOrChangeOrderDefault();
            clone.binary = this.binary;
            return clone;
        }

        private CreateOrChangeOrderDefault()
        {
            instanceId = ++nextInstanceId;
        }

	    public bool IsPending
	    {
            get
            {
                return binary.orderState == OrderState.Pending || binary.orderState == OrderState.PendingNew ||
                       binary.orderState == OrderState.Expired; 
            }
	    }

        public CreateOrChangeOrderDefault(OrderAction orderAction, SymbolInfo symbol, LogicalOrder logical, OrderSide side, int remainingSize, int cumulativeSize, double price)
            : this(OrderState.Pending, symbol, logical, side, remainingSize, cumulativeSize, price)
		{
		    binary.action = orderAction;
            instanceId = ++nextInstanceId;
        }

        public CreateOrChangeOrderDefault(OrderState orderState, SymbolInfo symbol, LogicalOrder logical, OrderSide side, int remainingSize, int cumulativeSize, double price)
		{
            binary.action = OrderAction.Create;
			OrderState = orderState;
		    binary.lastModifyTime = Factory.Parallel.UtcNow;
			binary.symbol = symbol;
			binary.side = side;
			binary.type = logical.Type;
			binary.price = price;
			binary.remainingSize = remainingSize;
            binary.cumulativeSize = cumulativeSize;
            binary.completeSize = remainingSize + cumulativeSize;
            binary.logicalOrderId = logical.Id;
			binary.logicalSerialNumber = logical.SerialNumber;
			binary.tag = logical.Tag;
			binary.reference = null;
			binary.replacedBy = null;
		    binary.originalOrder = null;
			binary.brokerOrder = CreateBrokerOrderId();
		    binary.utcCreateTime = logical.UtcChangeTime;
            binary.orderFlags = logical.OrderFlags;
            instanceId = ++nextInstanceId;
        }

        public CreateOrChangeOrderDefault(OrderAction action, OrderState orderState, SymbolInfo symbol, OrderSide side, OrderType type, OrderFlags flags, double price, int remainingSize, int logicalOrderId, long logicalSerialNumber, long brokerOrder, string tag, TimeStamp utcCreateTime) 
            : this( action, orderState, symbol, side, type, flags, price, remainingSize, 0, remainingSize, logicalOrderId, logicalSerialNumber, brokerOrder, tag, utcCreateTime)
        {
            
        }

        public CreateOrChangeOrderDefault(OrderAction action, OrderState orderState, SymbolInfo symbol, OrderSide side, OrderType type, OrderFlags flags, double price, int remainingSize, int cumulativeSize, int completeSize, int logicalOrderId, long logicalSerialNumber, long brokerOrder, string tag, TimeStamp utcCreateTime)
	    {
            binary.action = action;
			OrderState = orderState;
		    binary.lastModifyTime = Factory.Parallel.UtcNow;
			binary.symbol = symbol;
			binary.side = side;
			binary.type = type;
			binary.price = price;
			binary.remainingSize = remainingSize;
            binary.completeSize = completeSize;
            binary.cumulativeSize = cumulativeSize;
            binary.logicalOrderId = logicalOrderId;
			binary.logicalSerialNumber = logicalSerialNumber;
			binary.tag = tag;
			binary.brokerOrder = brokerOrder;
			binary.reference = null;
			binary.replacedBy = null;
	        binary.originalOrder = null;
	        binary.orderFlags = flags;
			if( binary.brokerOrder == 0L) {
                binary.brokerOrder = CreateBrokerOrderId();
			}
	        binary.utcCreateTime = utcCreateTime;
            instanceId = ++nextInstanceId;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if( IsSynthetic)
            {
                sb.Append("Synthetic ");
            }
            sb.Append(binary.action);
            sb.Append(" ");
            sb.Append(binary.orderState);
            sb.Append(" ");
            switch (binary.action)
            {
                case OrderAction.Create:
                case OrderAction.Change:
                    sb.Append(binary.side);
                    sb.Append(" ");
                    sb.Append(binary.completeSize);
                    sb.Append(" remains ");
                    sb.Append(binary.remainingSize);
                    sb.Append(" ");
                    sb.Append(binary.type);
                    sb.Append(" ");
                    break;
                case OrderAction.Cancel:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected action: " + binary.action);
            }
            sb.Append(binary.symbol);
            if (binary.action != OrderAction.Cancel && binary.type != OrderType.Market)
            {
                sb.Append(" at ");
                sb.Append(binary.price);
            }
            sb.Append(" and logical id: ");
            sb.Append(binary.logicalOrderId);
            sb.Append("-");
            sb.Append(binary.logicalSerialNumber);
            if (binary.brokerOrder != null)
            {
                sb.Append(" broker: ");
                sb.Append(binary.brokerOrder);
            }
            if (binary.originalOrder != null)
            {
                sb.Append(" original: ");
                sb.Append(binary.originalOrder.BrokerOrder);
            }
            if (binary.replacedBy != null)
            {
                sb.Append(" replaced by: ");
                sb.Append(binary.replacedBy.BrokerOrder);
            }
            if (binary.tag != null)
            {
                sb.Append(" ");
                sb.Append(binary.tag);
            }
            if (binary.sequence != 0)
            {
                sb.Append(" sequence: ");
                sb.Append(binary.sequence);
            }
            sb.Append(" create ");
            sb.Append(binary.utcCreateTime);
            sb.Append(" last change: ");
            sb.Append(binary.lastModifyTime);
            return sb.ToString();
        }

        private static long lastId = 0L;
		private static long CreateBrokerOrderId() {
            if( lastId == 0L)
            {
                if( Factory.IsAutomatedTest)
                {
                    lastId = 111111111111L;
                }
                else
                {
                    lastId = Factory.Parallel.UtcNow.Internal;
                }
            }
			var longId = Interlocked.Increment(ref lastId);
			return longId;
		}
		
		public OrderType Type {
            get { return binary.type; }
            set { binary.type = value; }
		}
		
		public double Price {
            get { return binary.price; }
		}

        private void AssertAtomic()
        {
            //if (!PhysicalOrderStoreDefault.IsLocked)
            //{
            //    log.Error("Attempt to modify PhysicalOrder w/o locking PhysicalOrderStore first.\n" + Environment.StackTrace);
            //}
        }
		
		public int RemainingSize {
            get { return binary.remainingSize; }
            set
            {
                AssertAtomic();
                binary.remainingSize = value;
            }
		}
		
		public long BrokerOrder {
            get { return binary.brokerOrder; }
			set
			{
                AssertAtomic();
                binary.brokerOrder = value;
			}
		}

		
		public SymbolInfo Symbol {
            get { return binary.symbol; }
		}
		
		public int LogicalOrderId {
            get { return binary.logicalOrderId; }
		}
		
		public OrderSide Side {
            get { return binary.side; }
            set { binary.side = value; }
		}
		
		public OrderState OrderState {
            get { return binary.orderState; }
            set
            {
                AssertAtomic();
                if (value != binary.orderState)
                {
                    binary.orderState = value;
                }
            }
		}

		public string Tag {
            get { return binary.tag; }
		}
		
		public long LogicalSerialNumber {
            get { return binary.logicalSerialNumber; }
		}
		
		public object Reference {
            get { return binary.reference; }
            set
            {
                AssertAtomic();
                binary.reference = value;
            }
		}

		public CreateOrChangeOrder ReplacedBy {
            get { return binary.replacedBy; }
            set
            {
                AssertAtomic();
                binary.replacedBy = value;
            }
		}

        public override bool Equals(object obj)
        {
            if( ! (obj is CreateOrChangeOrder))
            {
                return false;
            }
            var other = (CreateOrChangeOrder) obj;
            return binary.brokerOrder == other.BrokerOrder;
        }

        public void ResetLastChange()
        {
            binary.lastModifyTime = Factory.Parallel.UtcNow;
        }

        public void ResetLastChange(TimeStamp lastChange)
        {
            binary.lastModifyTime = lastChange;
        }

        public override int GetHashCode()
        {
            return binary.brokerOrder.GetHashCode();
        }

	    public TimeStamp LastModifyTime
	    {
            get { return binary.lastModifyTime; }
	    }

	    public TimeStamp UtcCreateTime
	    {
            get { return binary.utcCreateTime; }
            set
            {
                AssertAtomic();
                binary.utcCreateTime = value;
            }
        }

	    public OrderAction Action
	    {
            get { return binary.action; }
	    }

        public CreateOrChangeOrder OriginalOrder
	    {
            get { return binary.originalOrder; }
            set
            {
                AssertAtomic();
                binary.originalOrder = value;
            }
	    }

        public int Sequence
        {
            get { return binary.sequence; }
            set
            {
                AssertAtomic();
                binary.sequence = value;
            }
        }

        public TimeStamp LastReadTime
        {
            get { return binary.lastReadTime; }
            set { binary.lastReadTime = value; }
        }

        public bool IsSynthetic
        {
            get { return (binary.orderFlags & OrderFlags.IsSynthetic) > 0; }
            set
            {
                if( value)
                {
                    binary.orderFlags |= OrderFlags.IsSynthetic;
                }
                else
                {
                    binary.orderFlags ^= OrderFlags.IsSynthetic;
                }
            }
        }


        public bool IsTouch
        {
            get { return (binary.orderFlags & OrderFlags.IsTouch) > 0; }
            set
            {
                if (value)
                {
                    binary.orderFlags |= OrderFlags.IsTouch;
                }
                else
                {
                    binary.orderFlags ^= OrderFlags.IsTouch;
                }
            }
        }

        public bool OffsetTooLateToChange
        {
            get { return (binary.orderFlags & OrderFlags.OffsetTooLateToChange) > 0; }
        }

	    public OrderFlags OrderFlags
        {
            get { return binary.orderFlags; }
        }

        public int CompleteSize
        {
            get { return binary.completeSize; }
            set { binary.completeSize = value; }
        }

        public int CumulativeSize
        {
            get { return binary.cumulativeSize; }
            set { binary.cumulativeSize = value; }
        }

	    public int CancelCount
        {
            get { return binary.cancelCount; }
            set { binary.cancelCount = value; }
        }

        public int PendingCount
        {
            get { return binary.pendingCount; }
            set { binary.pendingCount = value; }
        }
    }
}