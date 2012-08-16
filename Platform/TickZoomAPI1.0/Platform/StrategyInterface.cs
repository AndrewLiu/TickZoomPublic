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

namespace TickZoom.Api
{
	public interface StrategyInterface : ModelInterface
	{
		string OnOptimizeResults();
		
		[Obsolete("Please, implement OnGetFitness() method instead.")]
		double Fitness {
			get;
		}
		
		PositionInterface Position {
			get;
		}
		
		ResultInterface Result {
			get;
		}
		
		double OnGetFitness();
		bool OnWriteReport(string folder);
		string OnGetOptimizeHeader(Dictionary<string,object> optimizeValues);
		string OnGetOptimizeResult(Dictionary<string,object> optimizeValues);

        OrderManager OrderManager { get; set; }

	 	Iterable<LogicalOrder> AllOrders { get; }
	 	Iterable<LogicalOrder> ActiveOrders { get; }
		
		void OrderModified( LogicalOrder order);

		bool IsActiveOrdersChanged { get; set; }
		
		bool TryGetOrderById( int id, out LogicalOrder order);
		void AddOrder( LogicalOrder order);
	}
}
