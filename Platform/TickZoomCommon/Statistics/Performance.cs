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
using System.IO;
using System.Security.Cryptography;
using System.Text;

using TickZoom.Api;
using TickZoom.Common;
using TickZoom.Interceptors;
using TickZoom.Reports;
using TickZoom.Transactions;

namespace TickZoom.Statistics
{
	public class Performance : StrategyInterceptor, LogAware
	{
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(Performance));
        private volatile bool debug;
        private volatile bool trace;
        public void RefreshLogLevel()
        {
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
		    barDataDebug = barDataLog.IsDebugEnabled;
		    tradeDebug = tradeLog.IsDebugEnabled;
		    transactionDebug = transactionLog.IsDebugEnabled;
		    statsDebug = statsLog.IsDebugEnabled;
        }
        private static readonly Log barDataLog = Factory.SysLog.GetLogger("BarDataLog");
        private volatile bool barDataDebug = barDataLog.IsDebugEnabled;
		private static readonly Log tradeLog = Factory.SysLog.GetLogger("TradeLog");
        private volatile bool tradeDebug = tradeLog.IsDebugEnabled;
		private static readonly Log transactionLog = Factory.SysLog.GetLogger("TransactionLog.Performance");
        private volatile bool transactionDebug = transactionLog.IsDebugEnabled;
		private static readonly Log statsLog = Factory.SysLog.GetLogger("StatsLog");
        private volatile bool statsDebug = statsLog.IsDebugEnabled;
		private TransactionPairs comboTrades;
		private TransactionPairsBinary comboTradesBinary;
		private bool graphTrades = true;
		private Equity equity;
		private ProfitLoss profitLoss;
		private PositionCommon position;
		private Model model;
		private DataHasher barHasher = new DataHasher();
		private DataHasher statsHasher = new DataHasher();
		
		public Performance(Model model)
		{
            log.Register(this);
			this.model = model;
			equity = new Equity(model,this);
			position = new PositionCommon(model);
		}
		
		public double GetCurrentPrice( double direction) {
			Tick tick = model.Ticks[0];
			if( direction > 0) {
				return tick.IsQuote ? tick.Bid : tick.Price;
			} else {
				return tick.IsQuote ? tick.Ask : tick.Price;
			}
		}
		
		EventContext context;
		
		public override void Intercept(EventContext context, EventType eventType, object eventDetail)
		{
			this.context = context;
			if( EventType.Initialize == eventType) {
				model.AddInterceptor( EventType.Close, this);
				model.AddInterceptor( EventType.LogicalFill, this);
				model.AddInterceptor( EventType.Tick, this);
				OnInitialize();
			}
			if( EventType.Close == eventType) {
				OnIntervalClose();
			}
			if( EventType.Tick == eventType) {
				TryUpdateComboTrades();
			}
			context.Invoke();
			if( EventType.LogicalFill == eventType ) {
				OnProcessFill((LogicalFill) eventDetail);
			}
		}
		
		public void OnInitialize()
		{ 
			comboTradesBinary  = new TransactionPairsBinary(model.Context.TradeData);
			comboTradesBinary.Name = "ComboTrades";
			profitLoss = model.Data.SymbolInfo.ProfitLoss;
			comboTrades  = new TransactionPairs(GetCurrentPrice,profitLoss,comboTradesBinary);
			profitLoss.Symbol = model.Data.SymbolInfo;

		}

		public bool OnProcessFill(LogicalFill fill)
		{
			if( debug) log.Debug(model + ": OnProcessFill: " + fill);
			if( fill.IsSimulated) {
				if( debug) log.Debug("Ignoring fill since it's a simulated fill meaning that the strategy already exited via a money management exit like stop loss or target profit, etc.");
				return true;
			}
			if( model is Portfolio) {
				var portfolio = (Portfolio) model;
				var portfolioPosition = portfolio.Result.Position;
				fill = new LogicalFillBinary( portfolioPosition.Current, fill.Recency, portfolioPosition.Price, fill.Time, fill.UtcTime, fill.OrderId, fill.OrderSerialNumber,fill.OrderPosition,false);
				if( debug) log.Debug("For portfolio, converted to fill: " + fill);
			}
			if( transactionDebug && !model.QuietMode && !(model is PortfolioInterface) ) {
				transactionLog.Debug( model.Name + "," + model.Data.SymbolInfo + "," + fill);
			}
			
			if( fill.Position != position.Current) {
				if( position.IsFlat) {
					if( model is Strategy && comboTradesBinary.Count > 0) {
						var comboTrade = comboTradesBinary.Tail;
						var strategy = model as Strategy;
						LogicalOrder filledOrder;
						strategy.TryGetOrderById( fill.OrderId, out filledOrder);
						if( !comboTrade.Completed && filledOrder.TradeDirection == TradeDirection.Change) {
							ChangeComboSize(fill);
						} else {
							EnterComboTrade(fill);
						}
					} else {
						EnterComboTrade(fill);
					}
				} else if( fill.Position == 0) {
					if( model is Strategy) {
						var strategy = model as Strategy;
						LogicalOrder filledOrder;
						strategy.TryGetOrderById( fill.OrderId, out filledOrder);
						if( filledOrder.TradeDirection != TradeDirection.Change) {
							ExitComboTrade(fill);
						} else
						{
						    ChangeComboSize(fill);
						}
					} else {
						ExitComboTrade(fill);
					}
				} else if( (fill.Position > 0 && position.IsShort) || (fill.Position < 0 && position.IsLong)) {
					// The signal must be opposite. Either -1 / 1 or 1 / -1
					if( model is Strategy) {
						var strategy = model as Strategy;
						LogicalOrder filledOrder;
						strategy.TryGetOrderById( fill.OrderId, out filledOrder);
						if( filledOrder.TradeDirection == TradeDirection.Change) {
							ChangeComboSize(fill);
						} else {
							ExitComboTrade(fill);
							EnterComboTrade(fill);
						}
					} else {
						ExitComboTrade(fill);
						EnterComboTrade(fill);
					}
				} else {
					// Instead it has increased or decreased position size.
					ChangeComboSize(fill);
				}
			} 
			position.Change(model.Data.SymbolInfo,fill);
			if( model is Strategy) {
				Strategy strategy = (Strategy) model;
				if( debug) log.Debug( "Changing strategy result position to " + position.Current);
				strategy.Result.Position.Copy(position);
			}

			return true;
		}
		
		public void EnterComboTrade(LogicalFill fill) {
			TransactionPairBinary pair = TransactionPairBinary.Create();
			pair.Enter(fill.Position, fill.Price, fill.Time, fill.PostedTime, model.Chart.ChartBars.BarCount, fill.OrderId, fill.OrderSerialNumber);
			comboTradesBinary.Add(pair);
			if( trace) {
				log.Trace( "Enter trade: " + pair);
			}
			if( model is Strategy) {
				Strategy strategy = (Strategy) model;
				LogicalOrder filledOrder;
				if( !strategy.TryGetOrderById( fill.OrderId, out filledOrder)) {
					throw new ApplicationException("A fill for order id: " + fill.OrderId + " was incorrectly routed to: " + strategy.Name);
				}
				strategy.OnEnterTrade(pair,fill,filledOrder);
			}
		}

		private void ChangeComboSize(LogicalFill fill) {
			TransactionPairBinary combo = comboTradesBinary.Tail;
			combo.ChangeSize(fill.Position,fill.Price);
			comboTradesBinary.Tail = combo;
			if( model is Strategy) {
				Strategy strategy = (Strategy) model;
				LogicalOrder filledOrder;
				if( !strategy.TryGetOrderById( fill.OrderId, out filledOrder)) {
					throw new ApplicationException("A fill for order id: " + fill.OrderId + " was incorrectly routed to: " + strategy.Name);
				}
				strategy.OnChangeTrade(combo,fill,filledOrder);
			}
		}
		
		public void ExitComboTrade(LogicalFill fill) {
			TransactionPairBinary comboTrade = comboTradesBinary.Tail;
			comboTrade.Exit( fill.Price, fill.Time, fill.PostedTime, model.Chart.ChartBars.BarCount, fill.OrderId, fill.OrderSerialNumber);
			comboTradesBinary.Tail = comboTrade;
		    var profitLoss2 = profitLoss as ProfitLoss2;
		    double pnl = 0D;
            if( profitLoss2 == null)
            {
                pnl = profitLoss.CalculateProfit(comboTrade.Direction, comboTrade.AverageEntryPrice, comboTrade.ExitPrice);
            }
            else
            {
                double costs;
                profitLoss2.CalculateProfit(comboTrade, out pnl, out costs);
                pnl = pnl - costs;
            }
			pnl = Math.Round(pnl,2);
			Equity.OnChangeClosedEquity( pnl);
			if( trace) {
				log.Trace( "Exit Trade: " + comboTrade);
			}
			if( tradeDebug && !model.QuietMode) tradeLog.Debug( model.Name + "," + Equity.ClosedEquity + "," + pnl + "," + comboTrade);
			if( model is Strategy) {
				Strategy strategy = (Strategy) model;
				LogicalOrder filledOrder;
				if( !strategy.TryGetOrderById( fill.OrderId, out filledOrder)) {
					throw new ApplicationException("A fill for order id: " + fill.OrderId + " was incorrectly routed to: " + strategy.Name);
				}
				if( model is Portfolio) {
					var portfolio = (Portfolio) model;
					portfolio.OnExitTrade();
				}
				strategy.OnExitTrade(comboTrade, fill,filledOrder);
			}
			if( model is Portfolio) {
				var portfolio = (Portfolio) model;
				portfolio.OnExitTrade();
			}
		}
		
		public bool OnIntervalClose()
		{
			if( barDataDebug && !model.QuietMode) {
				Bars bars = model.Bars;
				var time = bars.Time[0];
                var endTime = bars.EndTime[0];
                barHasher.Writer.Write(model.Name);
                barHasher.Writer.Write(time.Internal);
                barHasher.Writer.Write(endTime.Internal);
                barHasher.Writer.Write(bars.Open[0]);
				barHasher.Writer.Write(bars.High[0]);
				barHasher.Writer.Write(bars.Low[0]);
				barHasher.Writer.Write(bars.Close[0]);
				barHasher.Writer.Write(bars.Volume[0]);
				barHasher.Update();
				
				var sb = new StringBuilder();
				sb.Append(model.Name);
				sb.Append(",");
				sb.Append(time);
				sb.Append(",");
                sb.Append(endTime);
                sb.Append(",");
                sb.Append(bars.Open[0]);
				sb.Append(",");
				sb.Append(bars.High[0]);
				sb.Append(",");
				sb.Append(bars.Low[0]);
				sb.Append(",");
				sb.Append(bars.Close[0]);
				sb.Append(",");
				sb.Append(bars.Volume[0]);
				barDataLog.Debug( sb.ToString());
			}
			if( statsDebug && !model.QuietMode) {
				Bars bars = model.Bars;
				TimeStamp time = bars.Time[0];
				StringBuilder sb = new StringBuilder();
				statsHasher.Writer.Write(time.Internal);
				statsHasher.Writer.Write(equity.ClosedEquity);
				statsHasher.Writer.Write(equity.OpenEquity);
				statsHasher.Writer.Write(equity.CurrentEquity);
				statsHasher.Update();
				sb.Append(model.Name);
				sb.Append(",");
				sb.Append(time);
				sb.Append(",");
				sb.Append(equity.ClosedEquity);
				sb.Append(",");
				sb.Append(equity.OpenEquity);
				sb.Append(",");
				sb.Append(equity.CurrentEquity);
				statsLog.Debug( sb.ToString());
			}
			return true;
		}
		
		public string GetBarsHash() {
			return barHasher.GetHash();
		}
		
		public string GetStatsHash() {
			return statsHasher.GetHash();
		}
		
		public Equity Equity {
			get { return equity; }
			set { equity = value; }
		}
		
		public bool WriteReport(string name, string folder) {
			name = name.StripInvalidPathChars();
			TradeStatsReport tradeStats = new TradeStatsReport(this);
			tradeStats.WriteReport(name, folder);
			StrategyStats stats = new StrategyStats(ComboTrades);
			Equity.WriteReport(name,folder,stats);
			IndexForReport index = new IndexForReport(this);
			index.WriteReport(name, folder);
			return true;
		}
			
		/// <summary>
		/// Obsolete. Please use the ProfitLoss interface
		/// </summary>
		[Obsolete("Please use the ProfitLoss interface instead.",true)]
		public double Slippage {
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}
		
		/// <summary>
		/// Obsolete. Please use the ProfitLoss interface instead.
		/// </summary>
		[Obsolete("Please use the ProfitLoss interface instead.",true)]
		public double Commission {
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException();  }
		}
		
		public PositionCommon Position {
			get { return position; }
			set { position = value; }
		}
		
#region Obsolete Methods		
		
		[Obsolete("Use WriteReport(name,folder) instead.",true)]
		public void WriteReport(string name,StreamWriter writer) {
			throw new NotImplementedException();
		}

		[Obsolete("Please use ComboTrades instead.",true)]
    	public TransactionPairs TransactionPairs {
			get { return null; }
		}
		
		[Obsolete("Please use Performance.Equity.Daily instead.",true)]
		public TransactionPairs CompletedDaily {
			get { return Equity.Daily; }
		}

		[Obsolete("Please use Performance.Equity.Weekly instead.",true)]
		public TransactionPairs CompletedWeekly {
			get { return Equity.Weekly; }
		}
		
		private void TryUpdateComboTrades() {
			if( comboTradesBinary != null && comboTradesBinary.Count > 0) {
				TransactionPairBinary binary = comboTradesBinary.Tail;
				binary.TryUpdate(model.Ticks[0]);
				comboTradesBinary.Tail = binary;
			}
		}

		public TransactionPairs ComboTrades {
			get { 
				TryUpdateComboTrades();
				return comboTrades;
			}
		}
		
		[Obsolete("Please use Performance.Equity.Monthly instead.",true)]
		public TransactionPairs CompletedMonthly {
			get { return Equity.Monthly; }
		}
	
		[Obsolete("Please use Performance.Equity.Yearly instead.",true)]
		public TransactionPairs CompletedYearly {
			get { return Equity.Yearly; }
		}
		
		[Obsolete("Please use Performance.ComboTrades instead.",true)]
		public TransactionPairs CompletedComboTrades {
			get { return ComboTrades; }
		}
		
		[Obsolete("Please use TransactionPairs instead.",true)]
		public TransactionPairs Trades {
			get { return TransactionPairs; }
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public TransactionPairs Daily {
			get { return Equity.Daily; }
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public TransactionPairs Weekly {
			get { return Equity.Weekly; }
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public TransactionPairs Monthly {
			get { return Equity.Monthly; }
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public TransactionPairs Yearly {
			get { return Equity.Yearly; }
		}
		
		public bool GraphTrades {
			get { return graphTrades; }
			set { graphTrades = value; }
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public double ProfitToday {
			get { return Equity.CurrentEquity; }
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public double ProfitForWeek {
			get { return Equity.ProfitForWeek; }
		}

		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public double ProfitForMonth {
			get { return Equity.ProfitForMonth; }
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public double StartingEquity {
			get { return Equity.StartingEquity; }
			set { Equity.StartingEquity = value; }
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public double CurrentEquity {
			get { 
				return Equity.CurrentEquity;
			}
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public double ClosedEquity {
			get {
				return Equity.ClosedEquity;
			}
		}
		
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public double OpenEquity {
			get {
				return Equity.OpenEquity;
			}
		}
		
		public StrategyStats CalculateStatistics() {
			return new StrategyStats(ComboTrades);
		}
		
		/// <summary>
		/// <b>Obsolete:</b> Please use the same property at Performance.Equity.* instead.
		/// </summary>
		[Obsolete("Please use the same property at Performance.Equity.* instead.",true)]
		public bool GraphEquity {
			get { return Equity.GraphEquity; }
			set { Equity.GraphEquity = value; }
		}
		
#endregion		

	}

	[Obsolete("Please user Performance instead.",true)]
	public class PerformanceCommon : Performance {
		public PerformanceCommon(Strategy strategy) : base(strategy)
		{
			
		}
	}
	
}