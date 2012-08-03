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
using System.Text;

using TickZoom.Api;

namespace TickZoom.Api
{
    [SerializeContract]
    public struct LogicalFillBinary {
        [SerializeMember(1)]
        public int position;
        [SerializeMember(2)]
        public double price;
        [SerializeMember(3)]
        public TimeStamp time;
        [SerializeMember(4)]
        public TimeStamp utcTime;
        [SerializeMember(5)]
        public TimeStamp postedTime; 
        [SerializeMember(6)]
        public int orderId;
        [SerializeMember(7)]
        public long orderSerialNumber;
        [SerializeMember(8)]
        public int orderPosition;
        [SerializeMember(9)]
        public bool isExitStrategy;
        [SerializeMember(10)]
        public long recency;
        [SerializeMember(11)]
        public bool isComplete;
        [SerializeMember(12)]
        public bool isActual;

        public void Initialize(int position, long recency, double price, TimeStamp time, TimeStamp utcTime, int orderId, long orderSerialNumber, int orderPosition, bool isExitStrategy, bool isActual)
        {
            this.isActual = isActual;
            this.position = position;
            this.recency = recency;
            this.orderPosition = orderPosition;
            this.price = price;
            this.time = time;
            this.utcTime = utcTime;
            this.orderId = orderId;
            this.orderSerialNumber = orderSerialNumber;
            postedTime = new TimeStamp(1800,1,1);
            this.isExitStrategy = isExitStrategy;
            isComplete = false;
        }

        public int OrderId {
            get { return orderId; }
        }
		
        public long OrderSerialNumber {
            get { return orderSerialNumber; }
        }
		
        public int OrderPosition {
            get { return orderPosition; }
        }
		
        public TimeStamp Time {
            get { return time; }
        }

        public TimeStamp UtcTime {
            get { return utcTime; }
        }
		
        public TimeStamp PostedTime {
            get { return postedTime; }
            set { postedTime = value; }
        }
		
        public double Price {
            get { return price; }
        }

        public int Position {
            get { return position; }
        }
		
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append( orderId);
            sb.Append( ",");
            sb.Append( orderSerialNumber);
            sb.Append( ",");
            sb.Append( orderPosition);
            sb.Append( ",");
            sb.Append( price);
            sb.Append( ",");
            sb.Append( position);
            sb.Append( ",");
            sb.Append( time);
            sb.Append( ",");
            sb.Append( utcTime);
            sb.Append( ",");
            sb.Append( postedTime);
            sb.Append(", Recency ");
            sb.Append(recency);
            return sb.ToString();
        }

        public bool IsExitStrategy {
            get { return isExitStrategy; }
        }

        public long Recency
        {
            get { return recency; }
        }

        public bool IsComplete
        {
            get { return isComplete; }
            set { isComplete = value; }
        }

        public bool IsActual
        {
            get { return isActual; }
        }
    }
}
