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
using System.IO;

namespace TickZoom.Api
{
	public interface AsyncHandler {
		object Instance { get; }
	}
	
	public interface AsyncReceiver : Receiver, AsyncHandler, IDisposable {
	}

    public struct EventItem
    {
        public SymbolInfo Symbol;
        public int EventType;
        public object EventDetail;
        public EventItem( SymbolInfo symbol, int eventType, object detail)
        {
            this.Symbol = symbol;
            this.EventType = eventType;
            this.EventDetail = detail;
        }
        public EventItem(SymbolInfo symbol, int eventType)
        {
            this.Symbol = symbol;
            this.EventType = eventType;
            this.EventDetail = null;
        }
        public override string ToString()
        {
            return Symbol + " " + (EventType) EventType;
        }
    }
	
	public interface Receiver : IDisposable
	{
	    ReceiveEventQueue GetQueue(SymbolInfo symbol);
	}
	
	public interface Serializable {
        /// <summary>
        /// Deserialize.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>the version number of the serialized object.</returns>
		int FromReader(MemoryStream reader);
		void ToWriter(MemoryStream memory);
	}
	
	public interface Serializer  {
		object FromReader(MemoryStream reader);
		void ToWriter(object eventDetail, MemoryStream memory);
		int EventType {
			get;
		}
	}
	
	public enum BrokerState {
		Disconnected,
		Connected
	}
	
	public enum ReceiverState {
		Start,
		Ready,
		Historical,
		RealTime,
		Stop
	}
}
