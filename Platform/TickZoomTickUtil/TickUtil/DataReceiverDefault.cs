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
using TickZoom.Api;

//using TickZoom.Api;

namespace TickZoom.TickUtil
{
	public class DataReceiverDefault : Receiver {
	   	static readonly Log log = Factory.SysLog.GetLogger(typeof(DataReceiverDefault));
	   	readonly bool debug = log.IsDebugEnabled;
	    private TickQueue readQueue = new TickQueueImpl("DataReceiverDefault",100000);
        Provider sender;
        Pool<TickBinaryBox> tickPool = Factory.TickUtil.TickPool();
	    private static readonly bool captureEvents = Factory.Provider.EventLog.CheckEnabled(log);
	    private int receiverId;
        
		private ReceiverState receiverState = ReceiverState.Ready;
		
		public ReceiverState OnGetReceiverState(SymbolInfo symbol) {
			return receiverState;
		}
		
		public DataReceiverDefault(Provider sender) {
			this.sender = sender;
			readQueue.StartEnqueue = Start;
			if( captureEvents) {
				receiverId = Factory.Provider.EventLog.GetReceiverId(this);
			}
		}
		
		private void Start() {
			sender.SendEvent(this,null,(int)EventType.Connect,null);
		}
		
		public bool OnEvent(SymbolInfo symbol, int eventType, object eventDetail) {
			bool result = false;
			switch( (EventType) eventType) {
				case EventType.Tick:
					var binary = (TickBinaryBox) eventDetail;
			        do
			        {
			            if (readQueue.TryEnqueue(ref binary.TickBinary))
			            {
			                result = true;
			                tickPool.Free(binary);
			                break;
			            }
			        } while (!readQueue.IsFull);
					break;
				case EventType.EndHistorical:
					result = readQueue.TryEnqueue(EventType.EndHistorical, symbol);
					break;
				case EventType.StartRealTime:
					result = readQueue.TryEnqueue(EventType.StartRealTime, symbol);
					break;
				case EventType.EndRealTime:
					result = readQueue.TryEnqueue(EventType.EndRealTime, symbol);
					break;
				case EventType.Error:
		    		result = readQueue.TryEnqueue(EventType.Error, symbol);
		    		break;
				case EventType.Terminate:
		    		result = readQueue.TryEnqueue(EventType.Terminate, symbol);
		    		break;
				case EventType.LogicalFill:
				case EventType.StartHistorical:
				case EventType.Initialize:
				case EventType.Open:
				case EventType.Close:
				case EventType.PositionChange:
				default:
		    		// Skip these event types.
		    		result = true;
					break;
			}
			return result;
		}
		
		public TickQueue ReadQueue {
			get { return readQueue; }
		}
		
		public void Dispose() {
			
		}
		
	}
}
