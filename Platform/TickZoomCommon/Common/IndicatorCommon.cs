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
using System.Diagnostics;
using System.Drawing;
using System.Reflection;

using TickZoom.Api;
using TickZoom.Statistics;
using System.Text;

namespace TickZoom.Common
{
	/// <summary>
	/// Description of IndicatorSupport.
	/// </summary>
	public class IndicatorCommon : Model, IndicatorInterface
	{
		private readonly Log instanceLog;
		private readonly bool instanceDebug;
		private readonly bool instanceTrace;
        private static readonly Log barDataLog = Factory.SysLog.GetLogger("TestLog.BarDataLog");
        private readonly bool barDataDebug = barDataLog.IsDebugEnabled;
        private DataHasher barHasher = new DataHasher();
		double startValue = Double.NaN;
		bool isStartValueSet = false;
		Interval fastUpdateInterval = null;
		Doubles output;
		Doubles input;
		object anyInput;
		
		private Performance performance;
		
		protected object AnyInput {
			set { anyInput = value; }
		}
		
		/// <summary>
		/// Create a new generic indicator. This allows you
		/// to control the calculation and features of your
		/// indicator within your strategy. You most often use
		/// this feature to try out a new indicator idea or for
		/// a very simple indicator. For better performance and
		/// organization, you should eventually move your
		/// indicator to a separate class.
		/// </summary>
		public IndicatorCommon()
		{
			instanceLog = Factory.SysLog.GetLogger(this.GetType());
			instanceDebug = instanceLog.IsDebugEnabled;
			instanceTrace = instanceLog.IsTraceEnabled;
			isIndicator = true;
			Drawing.GroupName = Name;
			if( fastUpdateInterval != null) {
				RequestUpdate(fastUpdateInterval);
			}
			RequestEvent(EventType.Tick);
		}

		public override void OnConfigure()
		{
            output = Doubles();
            if (anyInput == null)
            {
				input = Doubles(Bars.Close);
			} else {
				input = Doubles(anyInput);
			}
		}
		
		public sealed override bool OnBeforeIntervalOpen() {
			base.OnBeforeIntervalOpen();
			if( isStartValueSet) { Add(this[0]); }
			else { Add(startValue); isStartValueSet = true; }
            return true;
		}
		
		public sealed override bool OnBeforeIntervalOpen(Interval interval) {
			return base.OnBeforeIntervalOpen(interval);
		}
		
		public sealed override bool OnBeforeIntervalClose() {
			return base.OnBeforeIntervalClose();
		}
		
		public sealed override bool OnBeforeIntervalClose(Interval interval) {
			return base.OnBeforeIntervalClose(interval);
		}

		public override bool OnProcessTick(Tick tick)
		{
			if( Chart != null && Chart.IsDynamicUpdate) {
				Update();
				return true;
			} else {
				return false;
			}
		}
		
		public override bool OnIntervalClose()
		{
            if (barDataDebug && !QuietMode)
            {
                var bars = Bars;
                var time = bars.Time[0];
                var endTime = bars.EndTime[0];
                barHasher.Writer.Write(Name);
                barHasher.Writer.Write(time.Internal);
                barHasher.Writer.Write(endTime.Internal);
                barHasher.Writer.Write(bars.Open[0]);
                barHasher.Writer.Write(bars.High[0]);
                barHasher.Writer.Write(bars.Low[0]);
                barHasher.Writer.Write(bars.Close[0]);
                barHasher.Writer.Write(bars.Volume[0]);
                barHasher.Update();

                barDataLog.DebugFormat(LogMessage.LOGMSG636, Name, time, endTime, bars.Open[0], bars.High[0], bars.Low[0], bars.Close[0], bars.Volume[0]);
            }
            Update();
			return true;
		}
		
		public override bool OnIntervalClose(Interval period)
		{
			if( period.Equals(fastUpdateInterval)) {
				Update();
			}
			return true;
		}	
		
		public virtual void Update() {
		}
		
		[Browsable(false)]
		public override string Name {
			get { return base.Name; }
			set { base.Name = value; /* propogateName(); */ }
		}
		
		public double StartValue {
			get { return startValue; }
			set { startValue = value; }
		}
		
		public Doubles Input {
			get { return input; }
		}
		
		[Browsable(true)]
		public override DrawingInterface Drawing {
			get { return base.Drawing; }
			set { base.Drawing = value; }
		}

		[Browsable(true)]
		public Interval FastUpdateInterval {
			get { return fastUpdateInterval; }
			set { fastUpdateInterval = value; }
		}

		#region Indicator Value Properties & Methods
		[Browsable(false)]
		public int Count {
			get { return output.Count; }
		}
		
		[Browsable(false)]
		public int BarCount {
			get { return output.BarCount; }
		}
		
		[Browsable(false)]
		public int CurrentBar {
			get { return output.CurrentBar; }
		}
		
		public void Add(double value)
		{
			output.Add(value);
		}

		public double this[int position]
		{
			get { return output[position]; }
			set { output[position] = value; }
		}
		
		public void Clear()
		{
			output.Clear();
		}
		#endregion
		
		public override string ToString()
		{
			if( Drawing == null == Drawing.GroupName.Equals(Name) ) {
				return Name;
			} else {
				return Name + "." + Drawing.GroupName;
			}
		}
		
		public Log Log {
			get { return instanceLog; }
		}
		
		public bool IsDebug {
			get { return instanceDebug; }
		}
		
		public bool IsTrace {
			get { return instanceTrace; }
		}
		
		public Performance Performance {
			get { return performance; }
			set { performance = value; }
		}
		
		public void Release() {
			output.Release();
			input.Release();
		}
	}
    /// <summary>
    /// For testing values properly passed to indicators.
    /// </summary>
    public class IndicatorTest : IndicatorCommon
    {
        public override void Update()
        {
            this[0] = Input[0];
        }

    }
}
