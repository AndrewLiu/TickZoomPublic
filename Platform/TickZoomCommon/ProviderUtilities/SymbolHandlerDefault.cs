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

namespace TickZoom.Common
{
	public class SymbolHandlerDefault : SymbolHandler {
		private LatencyMetric salesLatency;
		private LatencyMetric quotesLatency;
		private static Log log = Factory.SysLog.GetLogger(typeof(SymbolHandlerDefault));
		private bool debug = log.IsDebugEnabled;
		private bool trace = log.IsTraceEnabled;
		private TickIO tickIO = Factory.TickUtil.TickIO();
		private Receiver receiver;
		private SymbolInfo symbol;
		private bool isTradeInitialized = false;
		private bool isQuoteInitialized = false;
		private double position = 0D;
		public int bidSize;
		public double bid = 0D;
		public int askSize;
		public double ask = 0D;
		public int lastSize;
		public double last = 0D;
        private OrderAlgorithm logicalOrderHandler;
        private bool isRunning = false;
        private Pool<TickBinaryBox> tickPool = Factory.TickUtil.TickPool();
        private TimeStamp time;
	    private int diagnoseMetric = Diagnose.RegisterMetric("Symbol Handler");
        
		public void Start()
		{
			isRunning = true;
		}
		
		public void Stop()
		{
			isRunning = false;
		}
        
		public SymbolHandlerDefault(SymbolInfo symbol, Receiver receiver) {
        	this.symbol = symbol;
			this.receiver = receiver;
			this.quotesLatency = new LatencyMetric( "SymbolHandler-Quotes-" + symbol.Symbol.StripInvalidPathChars());
			this.salesLatency = new LatencyMetric( "SymbolHandler-Trade-" + symbol.Symbol.StripInvalidPathChars());
		}
		
		bool errorWrongLevel1Type = false;
		public void SendQuote() {
			if( isQuoteInitialized || VerifyQuote()) {
				if( isRunning) {
					if( symbol.QuoteType != QuoteType.Level1) {
						if( !errorWrongLevel1Type) {
							log.Error( "Received " + QuoteType.Level1 + " quote but " + symbol + " is configured for QuoteType = " + symbol.QuoteType + " in the symbol dictionary.");
							errorWrongLevel1Type = true;
						}
					} else if( Bid == 0D ) {
						log.Error("Found quote bid was set to " + Bid + " so skipping this tick.");
						return;
					} else if( Ask == 0D ) {
						log.Error("Found quote ask was set to " + Ask + " so skipping this tick.");
						return;
					} else {
						tickIO.Initialize();
						tickIO.SetSymbol(symbol.BinaryIdentifier);
						tickIO.SetTime(Time);
						tickIO.SetQuote(Bid,Ask,(short)BidSize,(short)AskSize);
						var box = tickPool.Create();
					    var tickId = box.TickBinary.Id;
						box.TickBinary = tickIO.Extract();
					    box.TickBinary.Id = tickId;
						quotesLatency.TryUpdate( box.TickBinary.Symbol, box.TickBinary.UtcTime);
						while( !receiver.OnEvent(symbol,(int)EventType.Tick,box))
						{
						    Factory.Parallel.Yield();
						}
                        if( Diagnose.TraceTicks) { Diagnose.AddTick(diagnoseMetric, ref box.TickBinary); }
						if( trace) log.Trace("Sent quote for " + symbol + ": " + tickIO);
					}
				}
			}
		}
        
        public void AddPosition( double position) {
        	this.position += position;
        }
	        	
        public void SetPosition( double position) {
        	if( this.position != position) {
	        	this.position = position;
        	}
        }
        
		private bool VerifyQuote() {
			if(BidSize > 0 && Bid > 0 && AskSize > 0 && Ask > 0) {
				isQuoteInitialized = true;
			}
			return isQuoteInitialized;
		}
        
		private bool VerifyTrade() {
			if(LastSize > 0 & Last > 0) {
				isTradeInitialized = true;
			}
			return isTradeInitialized;
		}
        
		bool errorWrongTimeAndSalesType = false;
		bool errorNeverAnyLevel1Tick = false;
		public void SendTimeAndSales() {
			if( !isRunning ) {
				return;
			}
			if( symbol.TimeAndSales != TimeAndSales.ActualTrades) {
				if( !errorWrongTimeAndSalesType) {
					log.Error( "Received " + TimeAndSales.ActualTrades + " trade but " + symbol + " is configured for TimeAndSales = " + symbol.TimeAndSales + " in the symbol dictionary.");
					errorWrongTimeAndSalesType = true;
				}
				return;
			}
			if( !isTradeInitialized && !VerifyTrade()) {
				return;
			}
			if( symbol.QuoteType == QuoteType.Level1) {
				if( !isQuoteInitialized && !VerifyQuote()) {
					if( !errorNeverAnyLevel1Tick) {
						log.Warn( "Found a Trade tick w/o any " + QuoteType.Level1 + " quote yet but " + symbol + " is configured for QuoteType = " + symbol.QuoteType + " in the symbol dictionary.");
						errorNeverAnyLevel1Tick = true;
					}
				} else if( errorNeverAnyLevel1Tick) {
					log.Notice( "Okay. Found a Level 1 quote tick that resolves the earlier warning message.");
					errorNeverAnyLevel1Tick = false;
				}
			}
			if( Last == 0D) {
				log.Error("Found last trade price was set to " + Last + " so skipping this tick.");
				return;
			}
			if( symbol.TimeAndSales == TimeAndSales.ActualTrades) {
				tickIO.Initialize();
				tickIO.SetSymbol(symbol.BinaryIdentifier);
				tickIO.SetTime(Time);
				tickIO.SetTrade(Last,LastSize);
				if( symbol.QuoteType == QuoteType.Level1 && isQuoteInitialized && VerifyQuote()) {
					tickIO.SetQuote(Bid,Ask,(short)BidSize,(short)AskSize);
				}
				var box = tickPool.Create();
			    var tickId = box.TickBinary.Id;
				box.TickBinary = tickIO.Extract();
			    box.TickBinary.Id = tickId;
				if( tickIO.IsTrade && tickIO.Price == 0D) {
					log.Warn("Found trade tick with zero price: " + tickIO);
				}		
				salesLatency.TryUpdate( box.TickBinary.Symbol, box.TickBinary.UtcTime);
				while( !receiver.OnEvent(symbol,(int)EventType.Tick,box))
				{
				    Factory.Parallel.Yield();
				}
                if (Diagnose.TraceTicks) { Diagnose.AddTick(diagnoseMetric, ref box.TickBinary); }
                if (trace) log.Trace("Sent trade tick for " + symbol + ": " + tickIO);
			}
		}
		private long lastTime;
        
		public OrderAlgorithm LogicalOrderHandler {
			get { return logicalOrderHandler; }
			set { logicalOrderHandler = value; }
		}
		
		
		public double Position {
			get { return position; }
		}
		
		public int LastSize {
			get { return lastSize; }
			set { lastSize = value; }
		}
		
		public int BidSize {
			get { return bidSize; }
			set { bidSize = value; }
		}
		
		private void AssureValue(double value) {
			if( double.IsInfinity(value) || double.IsNaN(value) ) {
				throw new ApplicationException("Value must not be infinity or NaN.");
			}
		}
		
		public double Bid {
			get { return bid; }
			set { AssureValue(value); bid = value; }
		}
		
		public int AskSize {
			get { return askSize; }
			set { askSize = value; }
		}
		
		public double Ask {
			get { return ask; }
			set { AssureValue(value); ask = value; }
		}
		
		public double Last {
			get { return last; }
			set { if( last != value) {
					if( double.IsNaN(value) || value == 0D) {
						log.Error("Value was set to " + value + ".\n" + Environment.StackTrace);
					}
					last = value;
				}
			}
		}
		
		public bool IsRunning {
			get { return isRunning; }
		}
        
		public TimeStamp Time {
			get { return time; }
			set { time = value; }
		}
	}
}