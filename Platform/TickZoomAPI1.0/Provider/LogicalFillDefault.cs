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
using TickZoom.Api;

namespace TickZoom.Api
{
    [SerializeContract]
    public class LogicalFillDefault : LogicalFill
    {
        private LogicalFillBinary binary;

        public LogicalFillDefault()
        {
        }

        public LogicalFillDefault(int position, long recency, double price, TimeStamp time, TimeStamp utcTime, int orderId, long orderSerialNumber, int orderPosition, bool isExitStrategy, bool isActual)
        {
            binary.Initialize(position, recency, price, time, utcTime, orderId, orderSerialNumber, orderPosition, isExitStrategy, isActual);
        }

        public void Initialize(int position, long recency, double price, TimeStamp time, TimeStamp utcTime, int orderId, long orderSerialNumber, int orderPosition, bool isExitStrategy, bool isActual)
        {
            binary.Initialize(position,recency,price,time,utcTime,orderId,orderSerialNumber,orderPosition,isExitStrategy,isActual);
        }

        public static LogicalFillDefault Parse(string value)
        {
            string[] fields = value.Split(',');
            int field = 0;
            var orderId = int.Parse(fields[field++]);
            var orderSerialNumber = long.Parse(fields[field++]);
            var orderPosition = int.Parse(fields[field++]);
            var price = double.Parse(fields[field++]);
            var position = int.Parse(fields[field++]);
            var time = TimeStamp.Parse(fields[field++]);
            var utcTime = TimeStamp.Parse(fields[field++]);
            var postedTime = TimeStamp.Parse(fields[field++]);
            var fill = new LogicalFillDefault();
            fill.Initialize(position, 0, price, time, utcTime, orderId, orderSerialNumber, orderPosition, false, false);
            fill.binary.postedTime = postedTime;
            return fill;
        }

        public int OrderId
        {
            get { return binary.orderId; }
        }

        public long OrderSerialNumber
        {
            get { return binary.orderSerialNumber; }
        }

        public int OrderPosition
        {
            get { return binary.orderPosition; }
        }

        public TimeStamp Time
        {
            get { return binary.time; }
        }

        public TimeStamp UtcTime
        {
            get { return binary.utcTime; }
        }

        public TimeStamp PostedTime
        {
            get { return binary.postedTime; }
            set { binary.postedTime = value; }
        }

        public double Price
        {
            get { return binary.price; }
        }

        public int Position
        {
            get { return binary.position; }
        }

        public bool IsExitStrategy
        {
            get { return binary.isExitStrategy; }
        }

        public long Recency
        {
            get { return binary.recency; }
        }

        public bool IsComplete
        {
            get { return binary.isComplete; }
            set { binary.isComplete = value; }
        }

        public bool IsActual
        {
            get { return binary.isActual; }
        }

        public override string ToString()
        {
            return binary.ToString();
        }
    }
}
