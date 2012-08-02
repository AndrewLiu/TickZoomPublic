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
using System.Threading;

namespace TickZoom.Api
{
    [SerializeContract]
	public class PositionChangeDetail {
        [SerializeMember(1)]
        private SymbolInfo symbol;
        [SerializeMember(2)]
        private int position;
        [SerializeMember(3)]
        private long utcTime;
        [SerializeMember(4)]
        private long recency;
        [SerializeMember(5)]
        private ActiveList<LogicalOrder> orders;
        //[SerializeMember(6)]
        private ActiveList<StrategyPosition> strategyPositions;

        public PositionChangeDetail()
        {
            this.orders = new ActiveList<LogicalOrder>();
            this.strategyPositions = new ActiveList<StrategyPosition>();
        }

        public PositionChangeDetail(SymbolInfo symbol, int position, ActiveList<LogicalOrder> orders, ActiveList<StrategyPosition> strategyPositions, long utcTime, long recency)
        {
            this.symbol = symbol;
            this.position = position;
            this.orders = orders;
            this.strategyPositions = strategyPositions;
            this.utcTime = utcTime;
            this.recency = recency;
        }

        public int Position
        {
            get { return position; }
            set { position = value; }
        }

        public ActiveList<LogicalOrder> Orders
        {
            get { return orders; }
            set { orders = value; }
        }

        public SymbolInfo Symbol
		{
		    get { return symbol; }
		    set { symbol = value; }
		}

        public long UtcTime
        {
            get { return utcTime; }
            set { utcTime = value; }
        }

        public ActiveList<StrategyPosition> StrategyPositions
        {
            get { return strategyPositions; }
            set { strategyPositions = value; }
        }

        public long Recency
	    {
	        get { return recency; }
	        set { recency = value; }
	    }

        public override string ToString()
        {
            return "Recency " + recency + ", "  + symbol + ", Position " + position + ", Orders " + orders.Count;
        }
	}
}
