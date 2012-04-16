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
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using TickZoom.Api;
using TickZoom.Properties;

namespace TickZoom.Symbols
{
	[Serializable]
	public class SymbolProperties : PropertiesBase, ISymbolProperties
	{
        [Serializable]
        private struct SymbolBinary
        {
            public Elapsed sessionStart;
            public Elapsed sessionEnd;
            public PartialFillSimulation partialFillSimulation;
            public bool simulateTicks;
            public string symbol;
            public double minimumTick;
            public double fullPointValue;
            public int level2LotSize;
            public double level2Increment;
            public int level2LotSizeMinimum;
            public long binaryIdentifier;
            public SymbolInfo universalSymbol;
            public int chartGroup;
            public QuoteType quoteType;
            public TimeAndSales timeAndSales;
            public string displayTimeZone;
            public string timeZone;
            public bool useSyntheticMarkets;
            public bool useSyntheticLimits;
            public bool useSyntheticStops;
            public ProfitLoss profitLoss;
            public string commission;
            public string fees;
            public string slippage;
            public string destination;
            public double maxPositionSize;
            public double maxOrderSize;
            public double maxValidPrice;
            public int minimumTickPrecision;
            public FIXSimulationType fixSimulationType;
            public LimitOrderQuoteSimulation _limitOrderQuoteSimulation;
            public LimitOrderTradeSimulation _limitOrderTradeSimulation;
            public OptionChain optionChain;
            public TimeInForce timeInForce;
            public string symbolFile;
            public bool _disableRealtimeSimulation;
            public string account;
        }

	    private SymbolBinary binary;

        public SymbolProperties()
        {
            binary.sessionStart = new Elapsed(8, 0, 0);
            binary.sessionEnd = new Elapsed(16, 30, 0);
            binary.partialFillSimulation = PartialFillSimulation.PartialFillsTillComplete;
            binary.quoteType = QuoteType.Level1;
            binary.timeAndSales = TimeAndSales.ActualTrades;
            binary.useSyntheticMarkets = true;
            binary.useSyntheticLimits = true;
            binary.useSyntheticStops = true;
            binary.commission = "default";
            binary.fees = "default";
            binary.slippage = "default";
            binary.destination = "default";
            binary.maxPositionSize = double.MaxValue;
            binary.maxOrderSize = double.MaxValue;
            binary.maxValidPrice = double.MaxValue;
            binary._limitOrderQuoteSimulation = LimitOrderQuoteSimulation.OppositeQuoteTouch;
            binary._limitOrderTradeSimulation = LimitOrderTradeSimulation.TradeTouch;
            binary.optionChain = OptionChain.None;
            binary._disableRealtimeSimulation = false;
            binary.profitLoss = new ProfitLossDefault(this);
            binary.account = "default";
        }

		public SymbolProperties Copy()
	    {
	    	SymbolProperties result;
	    	
	        using (var memory = new MemoryStream())
	        {
	            var formatter = new BinaryFormatter();
	            formatter.Serialize(memory, this);
	            memory.Position = 0;
	
	            result = (SymbolProperties)formatter.Deserialize(memory);
	            memory.Close();
	        }
	
	        return result;
	    }
	    
		public override string ToString()
		{
            return binary.symbol == null ? "empty" : Symbol;
		}
		
		[Obsolete("Please create your data with the IsSimulateTicks flag set to true instead of this property.",true)]
		public bool SimulateTicks {
            get { return binary.simulateTicks; }
            set { binary.simulateTicks = value; }
		}
		
		public Elapsed SessionEnd {
            get { return binary.sessionEnd; }
            set { binary.sessionEnd = value; }
		}
		
		public Elapsed SessionStart {
            get { return binary.sessionStart; }
            set { binary.sessionStart = value; }
		}
		
		public double MinimumTick {
            get { return binary.minimumTick; }
			set {
                binary.minimumTick = value; 
                SetPrecision();
            }
		}

        private void SetPrecision()
        {
            var minimumTick = binary.minimumTick;
            binary.minimumTickPrecision = 0;
            while ((long)minimumTick != minimumTick)
            {
                minimumTick *= 10;
                binary.minimumTickPrecision++;
            }
        }

		public double FullPointValue {
            get { return binary.fullPointValue; }
            set { binary.fullPointValue = value; }
		}
	
		public string Symbol {
            get { return binary.symbol + (binary.account != "default" ? "!" + binary.account : ""); }
            set { binary.symbol = value; }
		}
		
		public int Level2LotSize {
            get { return binary.level2LotSize; }
            set { binary.level2LotSize = value; }
		}
		
		public double Level2Increment {
            get { return binary.level2Increment; }
            set { binary.level2Increment = value; }
		}
		
		public SymbolInfo UniversalSymbol {
            get { return binary.universalSymbol; }
            set { binary.universalSymbol = value; }
		}
		
		public long BinaryIdentifier {
            get { return binary.binaryIdentifier; }
            set { binary.binaryIdentifier = value; }
		}
		
		public override bool Equals(object obj)
		{
            return obj is SymbolInfo && ((SymbolInfo)obj).BinaryIdentifier == binary.binaryIdentifier;
		}
	
		public override int GetHashCode()
		{
            return binary.binaryIdentifier.GetHashCode();
		}
		
		public QuoteType QuoteType {
            get { return binary.quoteType; }
            set { binary.quoteType = value; }
		}
		
		public string DisplayTimeZone {
            get { return binary.displayTimeZone; }
            set { binary.displayTimeZone = value; }
		}

		public string TimeZone {
            get { return binary.timeZone; }
            set { binary.timeZone = value; }
		}
		
		public bool UseSyntheticMarkets {
            get { return binary.useSyntheticMarkets; }
            set { binary.useSyntheticMarkets = value; }
		}
		
		public bool UseSyntheticLimits {
            get { return binary.useSyntheticLimits; }
            set { binary.useSyntheticLimits = value; }
		}
		
		public bool UseSyntheticStops {
            get { return binary.useSyntheticStops; }
            set { binary.useSyntheticStops = value; }
		}
		
		public ProfitLoss ProfitLoss {
            get { return binary.profitLoss; }
            set { binary.profitLoss = value; }
		}
		
		public string Destination {
            get { return binary.destination; }
            set { binary.destination = value; }
		}

		public string Fees {
            get { return binary.fees; }
            set { binary.fees = value; }
		}
		
		public string Commission {
            get { return binary.commission; }
            set { binary.commission = value; }
		}
		
		public string Slippage {
            get { return binary.slippage; }
            set { binary.slippage = value; }
		}
		
		public double MaxPositionSize {
            get { return binary.maxPositionSize; }
            set { binary.maxPositionSize = value; }
		}
		
		public double MaxOrderSize {
            get { return binary.maxOrderSize; }
            set { binary.maxOrderSize = value; }
		}
		
		public TimeAndSales TimeAndSales {
            get { return binary.timeAndSales; }
            set { binary.timeAndSales = value; }
		}

		public int ChartGroup {
            get { return binary.chartGroup; }
            set { binary.chartGroup = value; }
		}
		
		public int Level2LotSizeMinimum {
            get { return binary.level2LotSizeMinimum; }
            set { binary.level2LotSizeMinimum = value; }
		}		
		
		public double MaxValidPrice {
            get { return binary.maxValidPrice; }
            set { binary.maxValidPrice = value; }
		}
		
		public bool Equals(SymbolInfo other)
		{
            return binary.binaryIdentifier == other.BinaryIdentifier;
		}

        public LimitOrderQuoteSimulation LimitOrderQuoteSimulation
	    {
            get { return binary._limitOrderQuoteSimulation; }
            set { binary._limitOrderQuoteSimulation = value; }
	    }

        public LimitOrderTradeSimulation LimitOrderTradeSimulation
	    {
            get { return binary._limitOrderTradeSimulation; }
            set { binary._limitOrderTradeSimulation = value; }
	    }

	    public int MinimumTickPrecision
	    {
            get { return binary.minimumTickPrecision; }
	    }

	    public FIXSimulationType FixSimulationType
	    {
            get { return binary.fixSimulationType; }
            set { binary.fixSimulationType = value; }
	    }

	    public OptionChain OptionChain
	    {
            get { return binary.optionChain; }
            set { binary.optionChain = value; }
	    }

	    public TimeInForce TimeInForce
	    {
            get { return binary.timeInForce; }
            set { binary.timeInForce = value; }
	    }

	    public string SymbolFile
	    {
	        get {
                if (binary.symbolFile == null)
                {
                    return binary.symbol;
                }
                else
                {
                    
                }
                return binary.symbolFile;
            }
            set { binary.symbolFile = value; }
	    }

	    public PartialFillSimulation PartialFillSimulation
	    {
            get { return binary.partialFillSimulation; }
            set { binary.partialFillSimulation = value; }
	    }

	    public bool DisableRealtimeSimulation {
            get { return binary._disableRealtimeSimulation; }
            set { binary._disableRealtimeSimulation = value; }
	    }

        public string Account
        {
            get { return binary.account; }
            set { binary.account = value; }
        }
    }
}
