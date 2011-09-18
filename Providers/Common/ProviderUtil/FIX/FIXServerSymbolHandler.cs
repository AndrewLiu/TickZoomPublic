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
using System.Threading;
using TickZoom.Api;

namespace TickZoom.FIX
{
	public class FIXServerSymbolHandler : IDisposable, LogAware {
		private static Log log = Factory.SysLog.GetLogger(typeof(FIXServerSymbolHandler));
        private volatile bool debug;
        private volatile bool trace;
        public void RefreshLogLevel()
        {
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
        }
        private FillSimulator fillSimulator;
		private TickReader reader;
		private Action<Message,SymbolInfo,Tick> onTick;
		private Task queueTask;
		private TickSync tickSync;
		private SymbolInfo symbol;
		private TickIO nextTick = Factory.TickUtil.TickIO();
		private bool isFirstTick = true;
		private bool isPlayBack = false;
		private long playbackOffset;
		private FIXSimulatorSupport fixSimulatorSupport;
		private LatencyMetric latency;
		private TrueTimer tickTimer;
		private long intervalTime = 1000000;
		private long prevTickTime;
		private bool isVolumeTest = false;
		private long tickCounter = 0;
	    private int diagnoseMetric;
		
		public FIXServerSymbolHandler( FIXSimulatorSupport fixSimulatorSupport, 
		    bool isPlayBack, string symbolString,
		    Action<Message,SymbolInfo,Tick> onTick,
		    Action<PhysicalFill> onPhysicalFill,
		    Action<CreateOrChangeOrder,bool,string> onRejectOrder) {
            log.Register(this);
			this.fixSimulatorSupport = fixSimulatorSupport;
			this.isPlayBack = isPlayBack;
			this.onTick = onTick;
			this.symbol = Factory.Symbol.LookupSymbol(symbolString);
			reader = Factory.TickUtil.TickReader();
			reader.Initialize("Test\\MockProviderData", symbolString);
			fillSimulator = Factory.Utility.FillSimulator( "FIX", Symbol, false);
			FillSimulator.OnPhysicalFill = onPhysicalFill;
			FillSimulator.OnRejectOrder = onRejectOrder;
			tickSync = SyncTicks.GetTickSync(Symbol.BinaryIdentifier);
            //tickSync.ForceClear();
			queueTask = Factory.Parallel.Loop("FIXServerSymbol-"+symbolString, OnException, ProcessQueue);
			tickTimer = Factory.Parallel.CreateTimer(queueTask,PlayBackTick);
			queueTask.Scheduler = Scheduler.RoundRobin;
			reader.ReadQueue.Connect( queueTask);
			queueTask.Start();
			latency = new LatencyMetric("FIXServerSymbolHandler-"+symbolString.StripInvalidPathChars());
			reader.ReadQueue.StartEnqueue();
		    initialCount = reader.ReadQueue.Count;
		    diagnoseMetric = Diagnose.RegisterMetric("Simulator");
		}

	    private int initialCount;
		
		private void HasItem( object source) {
			queueTask.IncreaseActivity();
		}
		
	    private void TryCompleteTick() {
	    	if( tickSync.Completed) {
		    	if( trace) log.Trace("TryCompleteTick() Next Tick");
		    	tickSync.Clear();
            }
            else if (tickSync.OnlyReprocessPhysicalOrders)
            {
                if (debug) log.Debug("Reprocess physical orders - " + tickSync);
                FillSimulator.ProcessOrders();
                tickSync.RemoveReprocessPhysicalOrders();
            }
            else if (tickSync.OnlyProcessPhysicalOrders)
            {
                if (debug) log.Debug("Process physical orders - " + tickSync);
                FillSimulator.StartTick(nextTick);
                FillSimulator.ProcessOrders();
                tickSync.RemoveProcessPhysicalOrders();
            }
        }
		
		public int ActualPosition {
			get {
				return (int) FillSimulator.ActualPosition;
			}
		}
		
		public void CreateOrder(CreateOrChangeOrder order) {
			FillSimulator.OnCreateBrokerOrder( order);
		}
		
		public void ChangeOrder(CreateOrChangeOrder order) {
			FillSimulator.OnChangeBrokerOrder( order);
        }

        public void CancelOrder(CreateOrChangeOrder order)
        {
			FillSimulator.OnCancelBrokerOrder(order);
        }
		
		public CreateOrChangeOrder GetOrderById(string clientOrderId) {
			return FillSimulator.GetOrderById( clientOrderId);
		}
		
		private Yield ProcessQueue() {
            LatencyManager.IncrementSymbolHandler();
            if (SyncTicks.Enabled)
            {
				if( !tickSync.TryLock()) {
					TryCompleteTick();
					return Yield.NoWork.Repeat;
				} else {
					if( trace) log.Trace("Locked tickSync for " + Symbol);
				}
			}
			if( tickStatus == TickStatus.None || tickStatus == TickStatus.Sent) {
				return Yield.DidWork.Invoke(DequeueTick);
			} else {
				return Yield.NoWork.Repeat;
			}
		}

        private long GetNextUtcTime( long utcTime)
        {
            if (isVolumeTest)
            {
                return prevTickTime + intervalTime;
            }
            else
            {
                return utcTime + playbackOffset;
            }
        }

        public class ReadQueueEmptyException : Exception {}

	    private bool alreadyEmpty = false;
	    private Integers queueCounts = Factory.Engine.Integers();
	    private TickIO currentTick = Factory.TickUtil.TickIO();
		private Yield DequeueTick() {
            LatencyManager.IncrementSymbolHandler();
            var result = Yield.NoWork.Repeat;
			var binary = new TickBinary();
			
			try {
                if( !alreadyEmpty && reader.ReadQueue.Count == 0)
                {
                    alreadyEmpty = true;
                    try
                    {
                        throw new ReadQueueEmptyException();
                    } catch {}
                    var sb = new StringBuilder();
                    for( var i=0; i<queueCounts.Count; i++)
                    {
                        sb.AppendLine(queueCounts[i].ToString());
                    }
                    log.Info("Simulator found empty read queue at tick " + tickCounter + ", initial count " + initialCount + ". Recent counts:");
                    if( debug) log.Debug("Recent counts:\n"+sb);
                }
                queueCounts.Add(reader.ReadQueue.Count);
				if( reader.ReadQueue.TryDequeue( ref binary))
				{
				    if( Diagnose.TraceTicks) Diagnose.AddTick(diagnoseMetric, ref binary);
                    alreadyEmpty = false;
					tickStatus = TickStatus.None;
					tickCounter++;
                    if( isFirstTick)
                    {
                        currentTick.Inject(binary);
                    }
                    else
                    {
                        currentTick.Inject(nextTick.Extract());
                    }
                    if (isPlayBack)
                    {
                        if (isFirstTick)
                        {
                            playbackOffset = fixSimulatorSupport.GetRealTimeOffset(binary.UtcTime);
                            prevTickTime = TimeStamp.UtcNow.Internal + 5000000;
                        }
                        binary.UtcTime = GetNextUtcTime(binary.UtcTime);
				   	    prevTickTime = binary.UtcTime;
						if( tickCounter > 10) {
							intervalTime = 1000;
						}
                    }
                    isFirstTick = false;
                    nextTick.Inject(binary);
				   	tickSync.AddTick(nextTick);
				   	if( !isPlayBack) {
				   		if( isFirstTick) {
						   	FillSimulator.StartTick( nextTick);
					   		isFirstTick = false;
					   	} else { 
					   		FillSimulator.ProcessOrders();
					   	}
				   	}
				   	if( trace) log.Trace("Dequeue tick " + nextTick.UtcTime + "." + nextTick.UtcTime.Microsecond);
				   	result = Yield.DidWork.Invoke(ProcessTick);
				}
			} catch( QueueException ex) {
				if( ex.EntryType != EventType.EndHistorical) {
					throw;
				}
			}
			return result;
		}
		
		public enum TickStatus {
			None,
			Timer,
			Sent,
		}

		private volatile TickStatus tickStatus = TickStatus.None;
		private Yield ProcessTick() {
            LatencyManager.IncrementSymbolHandler();
            var result = Yield.NoWork.Repeat;
			if( isPlayBack ) {
				switch( tickStatus) {
					case TickStatus.None:
						var overlapp = 300L;
                        var currentTime = TimeStamp.UtcNow;
                        if( tickTimer.Active) tickTimer.Cancel();
                        if (nextTick.UtcTime.Internal > currentTime.Internal + overlapp &&
                           tickTimer.Start(nextTick.UtcTime))
                        {
                            if (trace) log.Trace("Set next timer for " + nextTick.UtcTime + "." + nextTick.UtcTime.Microsecond + " at " + currentTime + "." + currentTime.Microsecond);
                            tickStatus = TickStatus.Timer;
                        }
                        else
                        {
                            if (nextTick.UtcTime.Internal < currentTime.Internal)
                            {
                                if (trace)
                                    log.Trace("Current time " + currentTime + " was less than tick time " +
                                              nextTick.UtcTime + "." + nextTick.UtcTime.Microsecond);
                                result = Yield.DidWork.Invoke(SendPlayBackTick);
                            }
                        }
				        break;
					case TickStatus.Sent:
						result = Yield.DidWork.Invoke(ProcessQueue);
						break;
					case TickStatus.Timer:
						break;
					default:
						throw new ApplicationException("Unknown tick status: " + tickStatus);
				}
				return result;
			} else {
				if( trace) log.Trace("Invoking ProcessOnTickCallBack");
				return Yield.DidWork.Invoke( ProcessOnTickCallBack);
			}
		}
		
		private Yield SendPlayBackTick() {
            LatencyManager.IncrementSymbolHandler();
            latency.TryUpdate(nextTick.lSymbol, nextTick.UtcTime.Internal);
		   	if( isFirstTick) {
			   	FillSimulator.StartTick( nextTick);
		   		isFirstTick = false;
		   	} else { 
		   		FillSimulator.ProcessOrders();
		   	}
			return Yield.DidWork.Invoke(ProcessOnTickCallBack);
		}

		private Message quoteMessage;
		private Yield ProcessOnTickCallBack() {
            LatencyManager.IncrementSymbolHandler();
            if (quoteMessage == null)
            {
                quoteMessage = fixSimulatorSupport.QuoteSocket.MessageFactory.Create();
            }
			onTick( quoteMessage, Symbol, nextTick);
			if( trace) log.Trace("Added tick to packet: " + nextTick.UtcTime);
			quoteMessage.SendUtcTime = nextTick.UtcTime.Internal;
            return Yield.DidWork.Invoke(TryEnqueuePacket);
		}

		private Yield TryEnqueuePacket() {
            LatencyManager.IncrementSymbolHandler();
            if (quoteMessage.Data.GetBuffer().Length == 0)
            {
				return Yield.NoWork.Return;
			}
		    while( !fixSimulatorSupport.QuotePacketQueue.EnqueueStruct(ref quoteMessage,quoteMessage.SendUtcTime))
		    {
		        if( fixSimulatorSupport.QuotePacketQueue.IsFull)
		        {
		            return Yield.NoWork.Repeat;
		        }
		    }
			if( trace) log.Trace("Enqueued tick packet: " + new TimeStamp(quoteMessage.SendUtcTime));
		    quoteMessage = fixSimulatorSupport.QuoteSocket.MessageFactory.Create();
            reader.ReadQueue.RemoveStruct();
            tickStatus = TickStatus.Sent;
            return Yield.DidWork.Return;
		}
		
		private Yield PlayBackTick() {
            var result = Yield.DidWork.Repeat;
			if( tickStatus == TickStatus.Timer) {
				if( trace) log.Trace("Sending tick from timer event: " + nextTick.UtcTime);
				result = Yield.DidWork.Invoke(SendPlayBackTick);
			}
			return result;
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

	    public FillSimulator FillSimulator
	    {
	        get { return fillSimulator; }
	    }

	    public SymbolInfo Symbol
	    {
	        get { return symbol; }
	    }
	}
}