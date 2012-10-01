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
using TickZoom.Symbols;

namespace TickZoom.Properties
{
    /// <summary>
	/// Description of StarterProperties.
	/// </summary>
	public class StarterProperties : PropertiesBase, Api.StarterProperties
	{
		private readonly static Log log = Factory.SysLog.GetLogger(typeof(StarterProperties));
		private readonly bool debug = log.IsDebugEnabled;
		private readonly bool trace = log.IsTraceEnabled;
		TickZoom.Api.TimeStamp startTime;
		TickZoom.Api.TimeStamp endTime;
        private Interval portfolioSyncInterval = new IntervalImpl(BarUnit.Day, 1);
        ChartProperties chartProperties;
		EngineProperties engineProperties;
	    private int testFinishedTimeout;
		
		List<SymbolProperties> symbolInfo = new List<SymbolProperties>();
        List<string> symbolList = new List<string>();
		
		public StarterProperties(ChartProperties chartProperties, EngineProperties engineProperties)
		{
			this.chartProperties = chartProperties;
			this.engineProperties = engineProperties;
			startTime = TimeStamp.MinValue;
			endTime = TimeStamp.MaxValue;
			try {
                IntervalDefault = new IntervalImpl(TickZoom.Api.BarUnit.Day, 1);
			} catch {
				
			}
		}
		
		public void TryAddSymbols(string value)
		{
            var factory = (SymbolFactoryImpl)Factory.Symbol;
            var library = factory.Library;
            if (debug) log.DebugFormat(LogMessage.LOGMSG411);
			value = value.StripWhiteSpace();
			value = value.StripInvalidPathChars();
			var symbolFileArray = value.Split(',');
			for( int i=0; i<symbolFileArray.Length; i++)
			{
			    var tempExpandedSymbol = symbolFileArray[i].Trim();
                if (symbolList.Contains(tempExpandedSymbol)) continue;
                if (!string.IsNullOrEmpty(tempExpandedSymbol))
                {
                    var original = library.GetSymbolProperties(tempExpandedSymbol);
					var symbol = original.Copy();
					symbol.ChartGroup = i+1;
					log.Info(symbol + " set to chart group " + symbol.ChartGroup);
					symbolInfo.Add(symbol);
					symbolList.Add(symbol.ExpandedSymbol);
				}
			}
		}
		
		/// <summary>
		/// Obsolete: Please use TryAddSymbols()
		/// </summary>
		[Obsolete("Please use TryAddSymbols() instead.",true)]
		public string Symbols {
			set { throw new NotImplementedException(); }
		}
				
		public TickZoom.Api.TimeStamp StartTime {
			get { return startTime; }
			set { startTime = value; }
		}
		
		public TickZoom.Api.TimeStamp EndTime {
			get { return endTime; }
			set { endTime = value; }
		}
		
		TickZoom.Api.Interval intervalDefault;
		public TickZoom.Api.Interval IntervalDefault {
			get { return intervalDefault; }
			set { intervalDefault = value;
				  chartProperties.IntervalChartBar = value; 
				  engineProperties.IntervalDefault = value; 
			}
		}
		
		public ISymbolProperties[] SymbolProperties {
			get { return symbolInfo.ToArray(); }
		}
		
		[Obsolete("Please use SymbolProperties instead since it is mutable meaning you can programmatically override values from whatever was set (or not set) in the symbol dictionary.",true)]
		public SymbolInfo[] SymbolInfo {
			get { return symbolInfo.ToArray(); }
		}
		
	    public int TestFinishedTimeout
	    {
	        get { return testFinishedTimeout; }
	        set { testFinishedTimeout = value; }
	    }

        public Interval PortfolioSyncInterval
        {
            get { return portfolioSyncInterval; }
            set { portfolioSyncInterval = value; }
        }
	}
}