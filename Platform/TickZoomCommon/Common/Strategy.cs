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
using System.ComponentModel;
using System.Text;
using System.Threading;
using TickZoom.Api;
using TickZoom.Interceptors;
using TickZoom.Statistics;

namespace TickZoom.Common
{

	public class Strategy : Model, StrategyInterface
	{
		PositionInterface position;
		private readonly Log instanceLog;
		private readonly bool debug;
		private readonly bool trace;
		private Result result;
        private OrderManager orderManager;
		private Dictionary<int,LogicalOrder> ordersHash = new Dictionary<int,LogicalOrder>();
		private ActiveList<LogicalOrder> allOrders = new ActiveList<LogicalOrder>();
		private ActiveList<LogicalOrder> activeOrders = new ActiveList<LogicalOrder>();
		private List<LogicalOrder> nextBarOrders = new List<LogicalOrder>();
		private bool isActiveOrdersChanged = false;
		private NodePool<LogicalOrder> nodePool = new NodePool<LogicalOrder>();
				
		OrderHandlers orders;
		ReverseCommon reverseActiveNow;
		ReverseCommon reverseNextBar;
		ChangeCommon changeActiveNow;
		ChangeCommon changeNextBar;
		ExitCommon exitActiveNow;
		EnterCommon enterActiveNow;
		ExitCommon exitNextBar;
		EnterCommon enterNextBar;
		Performance performance;
		ExitStrategy exitStrategy;
		FillManager preFillManager;
		FillManager postFillManager;
		
		public Strategy()
		{
		    var logName = this.GetType().FullName;
            if( this.GetType().Name != Name)
            {
                logName += "." + Name;
            }
			instanceLog = Factory.SysLog.GetLogger(logName);
			debug = instanceLog.IsDebugEnabled;
			trace = instanceLog.IsTraceEnabled;
			position = new PositionCommon(this);
			if( trace) instanceLog.Trace("Constructor");
			Chain.Dependencies.Clear();
			isStrategy = true;
			result = new Result(this);
			
			exitActiveNow = new ExitCommon(this);
			enterActiveNow = new EnterCommon(this);
			reverseActiveNow = new ReverseCommon(this);
			changeActiveNow = new ChangeCommon(this);
			changeNextBar = new ChangeCommon(this);
			changeNextBar.Orders = changeActiveNow.Orders;
			changeNextBar.IsNextBar = true;
			reverseNextBar = new ReverseCommon(this);
			reverseNextBar.Orders = reverseActiveNow.Orders;
			reverseNextBar.IsNextBar = true;
			exitNextBar = new ExitCommon(this);
			exitNextBar.Orders = exitActiveNow.Orders;
			exitNextBar.IsNextBar = true;
			enterNextBar = new EnterCommon(this);
			enterNextBar.Orders = enterActiveNow.Orders;
			enterNextBar.IsNextBar = true;
			orders = new OrderHandlers(enterActiveNow,enterNextBar,
			                           exitActiveNow,exitNextBar,
			                           reverseActiveNow,reverseNextBar,
			                           changeActiveNow,changeNextBar);
			
			// Interceptors.
			performance = new Performance(this);
		    exitStrategy = new ExitStrategy(this);
			
		    preFillManager = new FillManager(this);
			postFillManager = new FillManager(this);
			postFillManager.PostProcess = true;
			postFillManager.ChangePosition = exitStrategy.Position.Change;
			postFillManager.DoStrategyOrders = false;
			postFillManager.DoStrategyOrders = false;
			postFillManager.DoExitStrategyOrders = true;
		}
		
		public override void OnConfigure()
		{
			changeActiveNow.OnInitialize();
			changeNextBar.OnInitialize();
			reverseActiveNow.OnInitialize();
			reverseNextBar.OnInitialize();
			exitActiveNow.OnInitialize();
			enterActiveNow.OnInitialize();
			exitNextBar.OnInitialize();
			enterNextBar.OnInitialize();
			exitNextBar.OnInitialize();
			base.OnConfigure();

			BreakPoint.TrySetStrategy(this);
			AddInterceptor(preFillManager);
			AddInterceptor(performance.Equity);
			AddInterceptor(performance);
			AddInterceptor(exitStrategy);
			AddInterceptor(postFillManager);
		}
		
		[Obsolete("Please, use OnGetOptimizeResult() instead.",true)]
		public virtual string ToStatistics() {
			throw new NotImplementedException();
		}
		
		public virtual string OnGetOptimizeHeader(Dictionary<string,object> optimizeValues)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("Fitness");
			foreach( KeyValuePair<string,object> kvp in optimizeValues) {
				sb.Append(",");
				sb.Append(kvp.Key);
			}
			return sb.ToString();
		}
		
		public virtual string OnGetOptimizeResult(Dictionary<string,object> optimizeValues)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(OnGetFitness());
			foreach( KeyValuePair<string,object> kvp in optimizeValues) {
				sb.Append(",");
				sb.Append(kvp.Value);
			}
			return sb.ToString();
		}
		
		public override bool OnWriteReport(string folder)
		{
			return performance.WriteReport(Name,folder);
		}
		
		private void ActiveOrdersChanged(LogicalOrder order) {
			if( trace) {
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("Active Orders:");
			    var next = activeOrders.First;
			    for (var current = next; current != null; current = next)
			    {
			        next = current.Next;
			        var item = current.Value;
					sb.Append("        ");
					sb.AppendLine( item.ToString());
				}
				sb.AppendLine("NextBar Orders:");
				foreach( var item in nextBarOrders) {
					sb.Append("        ");
					sb.AppendLine( item.ToString());
				}
				instanceLog.Trace("Order #" + order.Id + " was modified while position = " + position.Current + "\n" + sb);
				sb.AppendLine();
			}
		}
		
		public void OrderModified( LogicalOrder order) {
			if( order.IsActive) {
				// Any change to an active order even if only 
				// a price change means the list change.
				IsActiveOrdersChanged = true;
				if( !activeOrders.Contains(order)) {
					var newNode = nodePool.Create(order);
					bool found = false;
					var next = activeOrders.First;
					for( var node = next; node != null; node = next) {
						next = node.Next;
						LogicalOrder other = node.Value;
						if( order.CompareTo(other) < 0) {
							activeOrders.AddBefore(node,newNode);
							found = true;
							break;
						}
					}
					if( !found) {
						activeOrders.AddLast(newNode);
					}
				}
			} else {
				var node = activeOrders.Find(order);
				if( node != null) {
					activeOrders.Remove(node);
					nodePool.Free(node);
					// Since this order became inactive, it
					// means the active list changed.
					IsActiveOrdersChanged = true;
				}
			}
			if( order.IsNextBar) {
				if( !nextBarOrders.Contains(order)) {
					nextBarOrders.Add(order);
				}
			} else {
				if( nextBarOrders.Contains(order)) {
					nextBarOrders.Remove(order);
				}
			}
			ActiveOrdersChanged(order);
		}
		
		[Browsable(true)]
		[Category("Strategy Settings")]		
		public override Interval IntervalDefault {
			get { return base.IntervalDefault; }
			set { base.IntervalDefault = value; }
		}
		
		[Browsable(false)]
		public Strategy Next {
			get { return Chain.Next.Model as Strategy; }
		}
		
		[Browsable(false)]
		public Strategy Previous {
			get { return Chain.Previous.Model as Strategy; }
		}
		
		[Browsable(false)]
		public override string Name {
			get { return base.Name; }
			set { base.Name = value; }
		}

		public OrderHandlers Orders {
			get { return orders; }
		}
		
		[Obsolete("Please user Orders.Exit instead.",true)]
		public ExitCommon ExitActiveNow {
			get { return exitActiveNow; }
		}
		
		[Obsolete("Please user Orders.Enter instead.",true)]
		public EnterCommon EnterActiveNow {
			get { return enterActiveNow; }
		}
		
		[Category("Strategy Settings")]
		public ExitStrategy ExitStrategy {
			get { return exitStrategy; }
			set { exitStrategy = value; }
		}
		
		[Category("Strategy Settings")]
		public Performance Performance {
			get { return performance; }
			set { performance = value;}
		}
		
		
		/// <summary>
		/// Obsolete. Please use the Level2LotSize property in the symbol dictionary instead.
		/// </summary>
		[Obsolete("Obsolete. Please use the Level2LotSize property in the symbol dictionary instead.",true)]
		public PositionSize PositionSize {
			get { return new PositionSize(); }
			set { }
		}

		public virtual double OnGetFitness() {
			EquityStats stats = Performance.Equity.CalculateStatistics();
			return stats.Daily.SortinoRatio;
		}
		
		public Log Log {
			get { return instanceLog; }
		}
		
		public bool IsDebug {
			get { return debug; }
		}
		
		public bool IsTrace {
			get { return trace; }
		}

        public virtual void OnEnterTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
			OnEnterTrade();
		}
		
		public virtual void OnEnterTrade() {
			
		}

        public virtual void OnChangeTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
			OnChangeTrade();
		}
		
		public virtual void OnChangeTrade() {
		}		
		
		public virtual void OnExitTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder) {
			OnExitTrade();
		}
		
		public virtual void OnExitTrade() {
		}
		
		public PositionInterface Position {
			get { return position; }
			set { position = value; }
		}
		
		public ResultInterface Result {
			get { return result; }
		}

		[Diagram(AttributeExclude=true)]
		public void AddOrder(LogicalOrder order)
		{
            if (OrderManager != null)
            {
                OrderManager.AddOrder(order);
            }
			allOrders.AddLast(order);
			ordersHash.Add(order.Id,order);
		}
		
		public Iterable<LogicalOrder> AllOrders {
			get {
				return allOrders;
			}
		}
		
		public bool TryGetOrderById(int id, out LogicalOrder order)
		{
			return ordersHash.TryGetValue(id,out order);
		}
		
		public Iterable<LogicalOrder> ActiveOrders {
			get {
				return activeOrders;
			}
		}
		
		public bool IsActiveOrdersChanged {
			get { return isActiveOrdersChanged; }
			set { isActiveOrdersChanged = value; }
		}
		
		public bool IsExitStrategyFlat {
			get { return exitStrategy.Position.IsFlat && position.HasPosition; }
		}

        public OrderManager OrderManager
        {
            get { return orderManager; }
            set { orderManager = value; }
        }

    }
	
	/// <summary>
	/// Obsolete. Please use the Level2LotSize property in the symbol dictionary instead.
	/// </summary>
	[Obsolete("Obsolete. Please use the Level2LotSize property in the symbol dictionary instead.",true)]
	public class PositionSize {
		/// <summary>
		/// Obsolete. Please use the Level2LotSize property in the symbol dictionary instead.
		/// </summary>
		[Obsolete("Obsolete. Please use the Level2LotSize property in the symbol dictionary instead.",true)]
		public int Size {
			get { return 0; } 
			set { }
		}
	}
		
	[Obsolete("Please user Strategy instead.",true)]
	public class StrategyCommon : Strategy {
		
	}
}