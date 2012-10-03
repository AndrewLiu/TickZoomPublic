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
using System.Drawing;
using TickZoom.Api;

namespace TickZoom.Common
{
	[Diagram(AttributeExclude=true)]
	public class Formula
	{
	    private Model model;
		Elapsed fridayClose = new Elapsed(16,0,0);
		Elapsed sundayOpen = new Elapsed(17,0,0);
		
		public Formula( Model model) {
			this.model = model;
		}
		
		#region Price and bar data related formulas
		public double Range(Bars bars, int index) {
			return bars.High[index] - bars.Low[index];
		}
		
		public bool IsInsideBar(Bars bars, int index) {
			return bars.High[index] <= bars.High[index+1] && bars.Low[index] >= bars.Low[index+1];
		}
		
		public bool IsOutsideBar(Bars bars, int index) {
			return bars.High[index] > bars.High[index+1] && bars.Low[index] < bars.Low[index+1];
		}
		
		public bool IsBullFlatBar(Bars bars, int index) {
			return bars.High[index] == bars.High[index+1] && bars.Low[index] > bars.Low[index+1];
		}
		
		public bool IsBearFlatBar(Bars bars, int index) {
			return bars.High[index] < bars.High[index+1] && bars.Low[index] == bars.Low[index+1];
		}
		
		public double Middle(Bars bars, int index) {
			return (bars.High[index] + bars.Low[index])/2;
		}
		
		public bool HasHigherMiddle(Bars bars, int index) {
			return bars.Count > 1 && !IsInsideBar(bars,index) && Middle(bars,index) > Middle(bars,index+1) && bars.Close[index] > Middle(bars,index+1) && bars.Close[index] > bars.Open[index];
		}
		
		public bool HasLowerMiddle(Bars bars, int index) {
			return bars.Count > 1 && !IsInsideBar(bars,index) && Middle(bars,index) < Middle(bars,index+1) && bars.Close[index] < Middle(bars,index+1) && bars.Close[index] < bars.Open[index];
		}
		
		public bool HasHigherClose(Bars bars, int index) {
			return bars.Count > 1 && bars.Close[index] > bars.Close[index+1];
		}
		
		public bool HasLowerClose(Bars bars, int index) {
			return bars.Count > 1 && bars.Close[index] < bars.Close[index+1];
		}
		
		public bool IsDown(Bars bars, int index) {
			return bars.Count > 1 && bars.Close[index] < bars.Open[index];
		}
		
		public bool IsUp(Bars bars, int index) {
			return bars.Count > 1 && bars.Close[index] > bars.Open[index];
		}
		#endregion
		
		public int TradesToday(TimeStamp timeStamp) {
			Strategy strategy = Model as Strategy;
			int year = timeStamp.Year;
			int month = timeStamp.Month;
			int day = timeStamp.Day;
			int count = 0;
			if( strategy != null) {
				for( int i=strategy.Performance.ComboTrades.Count-1; i>=0; i--) {
					var trade = strategy.Performance.ComboTrades[i];
					TimeStamp exitTime = trade.ExitTime;
					if( year == trade.ExitTime.Year &&
					    month == trade.ExitTime.Month &&
					    day == trade.ExitTime.Day) {
						count++;	
					}
					if( exitTime < timeStamp) break;
				}
			}
			return count;
		}

		/// <summary>
		/// This constructs a default indicator. This is most useful for
		/// putting indicator logic directly into your strategy.
		/// </summary>
		/// <returns></returns>
		public IndicatorCommon Indicator() {
			IndicatorCommon indicator = new IndicatorCommon();
			Model.AddDependency(indicator );
			return indicator;
		}		

		public IndicatorCommon Line( double value, Color color) {
			return Line( value, color, null);
		}
		
		public IndicatorCommon Line( double value, Color color, string name) {
			IndicatorCommon line;
			line = new IndicatorCommon();
			line.Drawing.IsVisible = true;
			line.Drawing.PaneType = Model.Drawing.PaneType;
			line.Drawing.Color = Color.Green;
			line.Drawing.GroupName = Model.Drawing.GroupName;
			line.Name = name;
			line.StartValue = value;
			Model.AddIndicator(line);
			return line;
		}

		#region Trading related formulas like highest, lowest, etc.
		public double Highest(object values,int length) {
			return Highest(values,length,1);
		}
		
		public double Highest(object _values,int length,int displace) {
			Doubles values = Model.Doubles(_values);
			double max = int.MinValue;
			for( int i = displace; i<length+displace; i++) {
				max = Math.Max(values[i],max);
			}
			return max;
		}
		
		public double Lowest(object _values,int length) {
			return Lowest(_values,length,1);
		}
		public double Lowest(object _values,int length, int displace) {
			Doubles values = Model.Doubles(_values);
			double min = int.MaxValue;
			for( int i = displace; i<length+displace; i++) {
				min = Math.Min(values[i],min);
			}
			return min;
		}

		public double HighestP(object _values, int length) {
			Doubles values = Model.Doubles(_values);
			double hh = int.MinValue;
			for (int i = 0; i < length; i++) {
				if (values[i] > hh) hh = values[i];
			}
			return hh;
		}

		public double LowestP(object _values, int length) {
			Doubles values = Model.Doubles(_values);
			double ll = int.MaxValue;
			for (int i = 0; i < length; i++) {
				if (values[i] < ll) ll = values[i];
			}
			return ll;
		}
		
		public bool CrossesOver( object _price, double level) {
			return CrossesOver( _price, 0, level);
		}
		public bool CrossesOver( object _price, int displace, double level) {
			Doubles price = Model.Doubles(_price);
			return price[displace] > level && price[displace+1] <= level;
		}
		public bool CrossesOver( object _price1, object _price2) {
			return CrossesOver( _price1, 0, _price2, 0);
		}
		public bool CrossesOver( object _price1, int displace1, object _price2, int displace2) {
			Doubles price1 = Model.Doubles(_price1);
			Doubles price2 = Model.Doubles(_price2);
			return price1[displace1] > price2[displace2] && price1[displace1+1] <= price2[displace2+1];
		}
		public bool CrossesUnder( object _price, double level) {
			return CrossesUnder( _price, 0, level);
		}
		public bool CrossesUnder( object _price, int displace, double level) {
			Doubles price = Model.Doubles(_price);
			return price[displace] < level && price[displace+1] >= level;
		}
		public bool CrossesUnder( object _price1, object _price2) {
			return CrossesUnder( _price1, 0, _price2, 0);
		}
		public bool CrossesUnder( object _price1, int displace1, object _price2, int displace2) {
			Doubles price1 = Model.Doubles(_price1);
			Doubles price2 = Model.Doubles(_price2);
			return price1[displace1] < price2[displace2] && price1[displace1+1] >= price2[displace2+1];
		}
		#endregion
		
		#region Time related functions
		public bool IsForexWeek {
			get { TimeStamp dt = Model.Data.Ticks[0].Time;
				switch( dt.WeekDay) {
				case WeekDay.Friday:
					if( dt.TimeOfDay > fridayClose) {
						return false;
					} else {
						return true;
					}
				case WeekDay.Saturday:
					return false;
				case WeekDay.Sunday:
					if( dt.TimeOfDay < sundayOpen) {
						return false;
					} else {
						return true;
					}
				default:
					return true;
				}
			}
		}

	    public Model Model
	    {
	        get { return model; }
	    }

	    #endregion
	}
}
