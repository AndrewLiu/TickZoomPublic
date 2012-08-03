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
	public class LogicalFillBinaryBox : LogicalFill {
        [SerializeMember(1)]
        public LogicalFillDefault LogicalFillDefault;
		
		public int OrderId {
			get { return LogicalFillDefault.OrderId; }
		}

		public long OrderSerialNumber {
			get { return LogicalFillDefault.OrderSerialNumber; }
		}

		public int OrderPosition {
			get { return LogicalFillDefault.OrderPosition; }
		}

		public TimeStamp Time {
			get { return LogicalFillDefault.Time; }
		}

		public TimeStamp PostedTime {
			get { return LogicalFillDefault.PostedTime; }
		}

		public TimeStamp UtcTime {
			get { return LogicalFillDefault.UtcTime; }
		}

		public double Price {
			get { return LogicalFillDefault.Price; }
		}

		public int Position {
			get { return LogicalFillDefault.Position; }
		}
		
		public bool IsExitStrategy {
			get { return LogicalFillDefault.IsExitStrategy; }
		}

	    public long Recency
	    {
            get { return LogicalFillDefault.Recency; }
	    }

		public override string ToString()
		{
			return LogicalFillDefault.ToString();
		}

	    public bool IsComplete
	    {
            get { return LogicalFillDefault.IsComplete; }
	    }
	}
}
