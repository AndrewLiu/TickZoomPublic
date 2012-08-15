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
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom
{
	/// <summary>
	/// Description of Retrace.
	/// </summary>
	public class Retrace : IndicatorCommon
	{
		double lowest;
		double highest;
		double stretch;
		double adjustPercent = 0.50;
	    private Tick lastTick;
	    private TimeStamp startTime;
	    private TimeStamp limitTime;
        Interval resetInterval = Intervals.Day1;
	    private double minimumTick;
        public enum RetraceState
        {
            None,
            Flat,
            Increasing,
            Decreasing
        }

	    private RetraceState state;

        public override void OnInitialize()
        {
            minimumTick = Data.SymbolInfo.MinimumTick*10;
            Name = "Retrace";
        }

        public Retrace()
        {
            RequestUpdate(resetInterval);
        }

		public override bool OnProcessTick(Tick tick)
		{
		    lastTick = tick;
            switch( state)
            {
                case RetraceState.None:
                    Reset();
                    break;
                case RetraceState.Flat:
                    if (tick.Bid > highest)
                    {
                        Increase(tick);
                    }
                    if (tick.Ask < lowest)
                    {
                        Decrease(tick);
                    }
                    break;
                case RetraceState.Increasing:
                    if (tick.Bid > highest)
                    {
                        Increase(tick);
                    }
                    if (tick.Ask < this[0])
                    {
                        Switch(RetraceState.Decreasing);
                    }
                    break;
                case RetraceState.Decreasing:
                    if (tick.Ask < lowest)
                    {
                        Decrease(tick);
                    }
                    if (tick.Bid > this[0])
                    {
                        Switch(RetraceState.Increasing);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected state: " + state);
            }
            UpdateStretch(highest - lowest);
		    return true;
		}

	    private void UpdateStretch(double value)
	    {
            var elapsed = lastTick.UtcTime - startTime;
            var valueTicks = value / minimumTick;
            var stretchTicks = stretch / minimumTick;
            if (valueTicks > 50)
            {
                if( stretchTicks <= 50)
                {
                    limitTime = lastTick.UtcTime;
                }
            }
	        stretch = value;
	    }

	    private void Decrease(Tick tick)
	    {
            ChangeState(RetraceState.Decreasing);
            this[0] -= (lowest - tick.Ask) * adjustPercent;
	        lowest = tick.Ask;
            TryTimeStop();
        }

	    private void Increase(Tick tick)
	    {
            ChangeState(RetraceState.Increasing);
            this[0] += (tick.Bid - highest) * adjustPercent;
	        highest = tick.Bid;
	        TryTimeStop();
	    }

	    private long timeStopCount;
	    private void TryTimeStop()
	    {
            var elapsed = lastTick.UtcTime - startTime;
            //if (elapsed.TotalSeconds > 300 && stretch > 20 * minimumTick)
            //{
            //    AddStretchRecord(losingStretch);
            //    Reset();
            //    ++timeStopCount;
            //}
	    }

	    public double Stretch {
			get { return stretch; }
		}

        public void Reset()
        {
            if (lastTick != null)
            {
                ChangeState(RetraceState.Flat);
                limitTime = startTime = lastTick.UtcTime;
                var middle = (lastTick.Bid + lastTick.Ask) / 2;
                lowest = highest = middle;
                this[0] = middle;
            }
        }

        public class RetraceStretch
        {
            public double Stretch;
            public Elapsed Elapsed;
            public Elapsed LimitElapsed;
        }

	    public List<RetraceStretch> winningStretch = new List<RetraceStretch>();
        public List<RetraceStretch> losingStretch = new List<RetraceStretch>();
        
        private void Switch(RetraceState value)
		{
            if( lastTick != null)
            {
                AddStretchRecord(winningStretch);
                ChangeState(value);
            }
        }

	    private void AddStretchRecord(List<RetraceStretch> stretchList)
	    {
	        var stretchRetrace = new RetraceStretch
	                                 {
	                                     Stretch = stretch,
	                                     Elapsed = lastTick.UtcTime - startTime,
	                                     LimitElapsed = limitTime - startTime,
	                                 };
            stretchList.Add(stretchRetrace);
	        lowest = highest = this[0];
	        startTime = limitTime = lastTick.UtcTime;
	    }

	    public double RetracePercent {
			get { return 1 - adjustPercent; }
			set { adjustPercent = 1 - value; }
		}

        public override bool OnIntervalOpen(Interval interval)
        {
            if (interval.Equals(resetInterval))
            {
                //Reset();
            }
            return true;
        }

        private void ChangeState(RetraceState value)
	    {
	        switch (value)
	        {
	            case RetraceState.None:
	            case RetraceState.Flat:
	                break;
	            case RetraceState.Increasing:
	                break;
	            case RetraceState.Decreasing:
	                break;
	            default:
	                throw new ArgumentOutOfRangeException();
	        }
	        state = value;
	    }

        public override void OnEndHistorical()
        {
            Log.Info("WINNING");
            ReportStretch(winningStretch);
            Log.Info("LOSING");
            ReportStretch(losingStretch);
        }

	    private void ReportStretch(List<RetraceStretch> stretchList)
	    {
	        var maxTicks = 0L;
	        var maxMinutes = 0L;
	        foreach (var rs in stretchList)
	        {
	            var ticks = (long)(rs.Stretch / minimumTick);
	            if( ticks > maxTicks)
	            {
	                maxTicks = ticks;
	            }
	            var minutes = rs.Elapsed.TotalMinutes;
	            if( minutes > maxMinutes)
	            {
	                maxMinutes = minutes;
	            }
	        }

	        var ticksArray = new long[maxTicks+1];
	        foreach( var rs in stretchList)
	        {
	            var ticks = (long) (rs.Stretch/minimumTick);
	            if( ticks > 50)
	            {
	                Log.Info(ticks + "," + rs.Elapsed.TotalMinutes + "," + rs.LimitElapsed.TotalMinutes);
	            }
	            ++ticksArray[ticks];
	        }

	        var minutesArray = new long[maxMinutes + 1];
	        foreach (var rs in stretchList)
	        {
	            var minutes = rs.LimitElapsed.TotalMinutes;
	            //if (ticks > 50)
	            //{
	            //    Log.Info(ticks + "," + rs.Elapsed.TotalMinutes + "," + rs.LimitElapsed.TotalMinutes);
	            //}
	            ++minutesArray[minutes];
	        }

	        Log.Info("Ticks Array");
	        for (var i = 0; i < ticksArray.Length; i++)
	        {
	            if( ticksArray[i] > 0)
	            {
	                Log.Info(i + "," + ticksArray[i]);
	            }
	        }

	        Log.Info("Minutes Array");
	        for (var i = 0; i < minutesArray.Length; i++)
	        {
	            if (minutesArray[i] > 0)
	            {
	                Log.Info(i + "," + minutesArray[i]);
	            }
	        }
	        Log.Info("Time Stop Count " + timeStopCount);
	    }
	}
}
