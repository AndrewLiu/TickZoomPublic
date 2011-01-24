﻿#region Copyright
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
using TickZoom.Api;

namespace TickZoom.FIX
{
	public class FIXServerSymbolHandler : IDisposable {
		private static Log log = Factory.SysLog.GetLogger(typeof(FIXServerSymbolHandler));
		private static bool trace = log.IsTraceEnabled;
		private static bool debug = log.IsDebugEnabled;
		private FillSimulator fillSimulator;
		private TickReader reader;
		private Func<SymbolInfo,Tick,Yield> onTick;
		private Func<Yield> onHeartbeat;
		private Task queueTask;
		private TickSync tickSync;
		private SymbolInfo symbol;
		private TickIO nextTick = Factory.TickUtil.TickIO();
		private bool isFirstTick = true;
		private bool isPlayBack = false;
		private long playbackOffset;
		private TimeStamp heartbeatTimer;
		private bool firstHearbeat = true;
		private FIXSimulatorSupport fixSimulatorSupport;
		private LatencyMetric latency;
		
		public FIXServerSymbolHandler( FIXSimulatorSupport fixSimulatorSupport, 
			    bool isPlayBack, string symbolString,
			    Func<Yield> onHeartbeat, Func<SymbolInfo,Tick,Yield> onTick,
			    Action<PhysicalFill, int,int,int> onPhysicalFill,
			    Action<PhysicalOrder,string> onRejectOrder) {
			this.fixSimulatorSupport = fixSimulatorSupport;
			this.isPlayBack = isPlayBack;
			this.onHeartbeat = onHeartbeat;
			this.onTick = onTick;
			this.symbol = Factory.Symbol.LookupSymbol(symbolString);
			reader = Factory.TickUtil.TickReader();
			reader.Initialize("Test\\MockProviderData", symbolString);
			fillSimulator = Factory.Utility.FillSimulator( "FIX", symbol, false);
			fillSimulator.OnPhysicalFill = onPhysicalFill;
			fillSimulator.OnRejectOrder = onRejectOrder;
			tickSync = SyncTicks.GetTickSync(symbol.BinaryIdentifier);
			tickSync.ForceClear();
			queueTask = Factory.Parallel.Loop("FIXServerSymbol-"+symbolString, OnException, ProcessQueue);
			queueTask.Start();
			latency = new LatencyMetric("FIXServerSymbolHandler-"+symbolString.StripInvalidPathChars());
			firstHearbeat = true;
		}
		
	    private void TryCompleteTick() {
	    	if( tickSync.Completed) {
		    	if( trace) log.Trace("TryCompleteTick()");
		    	tickSync.Clear();
	    	} else if( tickSync.OnlyProcessPhysicalOrders) {
				fillSimulator.StartTick(nextTick);
				fillSimulator.ProcessOrders();
				tickSync.RemoveProcessPhysicalOrders();
	    	}
		}
		
		public int ActualPosition {
			get {
				return (int) fillSimulator.ActualPosition;
			}
		}
		
		public void CreateOrder(PhysicalOrder order) {
			fillSimulator.OnCreateBrokerOrder( order);
		}
		
		public void ChangeOrder(PhysicalOrder order, object origBrokerOrder) {
			fillSimulator.OnChangeBrokerOrder( order, origBrokerOrder);
		}
		
		public void CancelOrder(object origBrokerOrder) {
			fillSimulator.OnCancelBrokerOrder( symbol, origBrokerOrder);
		}
		
		public PhysicalOrder GetOrderById(string clientOrderId) {
			return fillSimulator.GetOrderById( clientOrderId);
		}
		
		private Yield ProcessQueue() {
			if( SyncTicks.Enabled) {
				if( !tickSync.TryLock()) {
					TryCompleteTick();
					return Yield.NoWork.Repeat;
				} else {
					if( trace) log.Trace("Locked tickSync for " + symbol);
				}
			}
			return Yield.DidWork.Invoke(DequeueTick);
		}

		private Yield DequeueTick() {
			var result = Yield.NoWork.Repeat;
			var binary = new TickBinary();
			
			try { 
				if( reader.ReadQueue.TryDequeue( ref binary)) {
				   	if( isPlayBack) {
						if( isFirstTick) {
							playbackOffset = fixSimulatorSupport.GetRealTimeOffset(binary.UtcTime);
						}
						binary.UtcTime += playbackOffset;
						var time = new TimeStamp( binary.UtcTime);
				   	} 
				   	nextTick.Inject( binary);
				   	tickSync.AddTick();
				   	if( !isPlayBack) {
					   	if( isFirstTick) {
						   	fillSimulator.StartTick( nextTick);
					   		isFirstTick = false;
					   	} else { 
					   		fillSimulator.ProcessOrders();
					   	}
				   	}
				   	if( trace) log.Trace("Dequeue tick " + nextTick.UtcTime);
				   	result = Yield.DidWork.Invoke(ProcessTick);
				}
			} catch( QueueException ex) {
				if( ex.EntryType != EventType.EndHistorical) {
					throw;
				}
			}
			return result;
		}
		
		private void IncreaseHeartbeat(TimeStamp currentTime) {
			heartbeatTimer = currentTime;
			heartbeatTimer.AddSeconds(30);
		}		

		private void TryRequestHeartbeat(TimeStamp currentTime) {
			if( firstHearbeat) {
				IncreaseHeartbeat(currentTime);
				firstHearbeat = false;
				return;
			}
			if( currentTime > heartbeatTimer) {
				IncreaseHeartbeat(currentTime);
				onHeartbeat();
			}
		}
		
		public enum TickStatus {
			None,
			Timer,
			Sent,
		}

		private volatile TickStatus tickStatus = TickStatus.None;
		private Yield ProcessTick() {
			var result = Yield.NoWork.Repeat;
			if( isPlayBack ) {
				var currentTime = TimeStamp.UtcNow;
				switch( tickStatus) {
					case TickStatus.None:
						var overlapp = 5000L;
						if( currentTime.Internal + overlapp <= nextTick.UtcTime.Internal) {
							Factory.Parallel.NextTimer(OnException,nextTick.UtcTime,PlayBackTick);
							if( trace) log.Trace("Set next timer for " + nextTick.UtcTime  + "." + nextTick.UtcTime.Microsecond + " at " + currentTime  + "." + currentTime.Microsecond);
							tickStatus = TickStatus.Timer;
						} else {
							if( trace) log.Trace("Current time " + currentTime + " was greater than tick time " + nextTick.UtcTime + "." + nextTick.UtcTime.Microsecond);
							SendPlayBackTick();
							result = Yield.DidWork.Return;
							try { 
								var binary = new TickBinary();
								if( reader.ReadQueue.TryDequeue( ref binary)) {
									binary.UtcTime += playbackOffset;
								   	nextTick.Inject( binary);
									if( trace) log.Trace("Found another tick on the queue at " + nextTick.UtcTime + "." + nextTick.UtcTime.Microsecond);
								   	tickSync.AddTick();
									result = Yield.DidWork.Repeat;
								}
							} catch( QueueException ex) {
								if( ex.EntryType != EventType.EndHistorical) {
									throw;
								}
							}
						}		
						break;
					case TickStatus.Sent:
						tickStatus = TickStatus.None;
						result = Yield.DidWork.Return;
						break;
				}
				TryRequestHeartbeat(currentTime);
				return result;
			} else {
				return onTick( symbol, nextTick);
			}
		}
		
		private void SendPlayBackTick() {
		   	if( isFirstTick) {
			   	fillSimulator.StartTick( nextTick);
		   		isFirstTick = false;
		   	} else { 
		   		fillSimulator.ProcessOrders();
		   	}
			var time = nextTick.UtcTime;
			var latencyUs = TimeStamp.UtcNow.Internal - nextTick.UtcTime.Internal;
			if( trace) log.Trace("Updating latency " + time + "." + time.Microsecond + " latency = " + latencyUs);
			latency.TryUpdate( nextTick.lSymbol, nextTick.UtcTime.Internal);
			onTick( symbol, nextTick);
		}
		
		private void PlayBackTick() {
			SendPlayBackTick();
			tickStatus = TickStatus.Sent;
			queueTask.Boost();
		}
		
		private void OnException( Exception ex) {
			// Attempt to propagate the exception.
			log.Error("Exception occurred", ex);
			Dispose();
		}
		
	 	protected volatile bool isDisposed = false;
	    public void Dispose() 
	    {
	        Dispose(true);
	        GC.SuppressFinalize(this);      
	    }
	
	    protected virtual void Dispose(bool disposing)
	    {
	       		if( !isDisposed) {
	            isDisposed = true;   
	            if (disposing) {
	            	if( debug) log.Debug("Dispose()");
	            	if( reader != null) {
	            		reader.Dispose();
	            	}
	            	if( queueTask != null) {
	            		queueTask.Stop();
	            	}
	            }
    		}
	    }    
	        
		public bool IsPlayBack {
			get { return isPlayBack; }
			set { isPlayBack = value; }
		}
	}
}