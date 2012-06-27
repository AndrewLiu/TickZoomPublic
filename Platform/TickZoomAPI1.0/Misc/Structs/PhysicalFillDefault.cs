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
using TickZoom.Api;

namespace TickZoom.Api
{
    [SerializeContract]
	public class PhysicalFillDefault : PhysicalFill
	{
        [SerializeMember(1)]
        private SymbolInfo symbol;
        [SerializeMember(2)]
        private int size;
        [SerializeMember(3)]
        private double price;
        [SerializeMember(4)]
        private TimeStamp time;
        [SerializeMember(5)]
        private TimeStamp utcTime;
        [SerializeMember(6)]
        private long brokerOrder;
        [SerializeMember(7)]
        private bool isExitStategy;
        [SerializeMember(8)]
        private int completeSize;
        [SerializeMember(9)]
        private int cumulativeSize;
        [SerializeMember(10)]
        private int remainingSize;
        [SerializeMember(11)]
        private bool isRealTime;
        [SerializeMember(12)]
        private bool isActual;

	    public PhysicalFillDefault(SymbolInfo symbol, int size, double price, TimeStamp time, TimeStamp utcTime, long brokerOrder, 
	                               bool isExitStategy, int completeSize, int cumulativeSize, int remainingSize, bool isRealTime, bool isActual)
	    {
	        this.symbol = symbol;
			this.size = size;
			this.price = price;
			this.time = time;
			this.utcTime = utcTime;
	        this.brokerOrder = brokerOrder;
			this.isExitStategy = isExitStategy;
	        this.completeSize = completeSize;
	        this.cumulativeSize = cumulativeSize;
	        this.remainingSize = remainingSize;
	        this.isRealTime = isRealTime;
	        this.isActual = isActual;
        }
		
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append( "Filled ");
			sb.Append( size );
			sb.Append( " at ");
			sb.Append( price);
			sb.Append( " on ");
			sb.Append( time);
			sb.Append( " for order: " );
			sb.Append( brokerOrder);
			return sb.ToString();
		}

	    public SymbolInfo Symbol
	    {
	        get { return symbol; }
	    }

	    public TimeStamp Time {
			get { return time; }
		}

		public TimeStamp UtcTime {
			get { return utcTime; }
		}

		public double Price {
			get { return price; }
		}

		public int Size {
			get { return size; }
		}

		public long BrokerOrder {
			get { return brokerOrder; }
		}
				
		public bool IsExitStategy {
			get { return isExitStategy; }
		}

	    public int CompleteSize
	    {
	        get { return completeSize; }
	    }

	    public int CumulativeSize
	    {
	        get { return cumulativeSize; }
	    }

	    public int RemainingSize
	    {
	        get { return remainingSize; }
	    }

	    public bool IsRealTime
	    {
	        get { return isRealTime; }
	    }

	    public bool IsActual
	    {
	        get { return isActual; }
	    }

	}
}
