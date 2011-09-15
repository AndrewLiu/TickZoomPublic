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
using System.IO;
using System.Security.Cryptography;

using TickZoom.Api;

namespace TickZoom.MBTQuotes
{
	[SkipDynamicLoad]
	public class MBTQuotesProvider : MBTQuoteProviderSupport, LogAware
	{
		private static Log log = Factory.SysLog.GetLogger(typeof(MBTQuotesProvider));
        private volatile bool debug;
        private volatile bool trace;
        public override void RefreshLogLevel()
        {
            base.RefreshLogLevel();
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
        }
        private Dictionary<long, SymbolHandler> symbolHandlers = new Dictionary<long, SymbolHandler>();
        private Dictionary<long, SymbolHandler> symbolOptionHandlers = new Dictionary<long, SymbolHandler>();	
		
		public MBTQuotesProvider(string name)
		{
		    log.Register(this);
			ProviderName = "MBTQuotesProvider";
			if( name.Contains(".config")) {
				throw new ApplicationException("Please remove .config from config section name.");
			}
			ConfigSection = name;
			RetryStart = 1;
			RetryIncrease = 1;
			RetryMaximum = 30;
			if( SyncTicks.Enabled) {
	  			HeartbeatDelay = int.MaxValue;
			} else {
	  			HeartbeatDelay = 10;
			}
        }
		
		public override void PositionChange(Receiver receiver, SymbolInfo symbol, double signal, Iterable<LogicalOrder> orders)
		{
		}
		
		public override void OnDisconnect()
		{
		}
		
		public override void OnRetry()
		{
		}
        
		public override Yield OnLogin()
		{
			Socket.MessageFactory = new MessageFactoryMbtQuotes();

		    Message message = Socket.MessageFactory.Create();
			string hashPassword = Hash(Password);
			string login = "L|100="+UserName+";133="+hashPassword+"\n";
			if( trace) log.Trace( "Sending: " + login);
            if (debug) log.Debug("Sending: " + login);
            message.DataOut.Write(login.ToCharArray());
			while( !Socket.TrySendMessage(message)) {
				if( IsInterrupted) return Yield.NoWork.Repeat;
				Factory.Parallel.Yield();
			}
			while( !Socket.TryGetMessage(out message)) {
				if( IsInterrupted) return Yield.NoWork.Repeat;
				Factory.Parallel.Yield();
			}
			message.BeforeRead();
			char firstChar = (char) message.Data.GetBuffer()[message.Data.Position];
			if( firstChar != 'G') {
				throw new ApplicationException("Invalid quotes login response: \n" + new string(message.DataIn.ReadChars(message.Remaining)));
			}
			if( trace) log.Trace( "Response: " + new string(message.DataIn.ReadChars(message.Remaining)));
            Socket.MessageFactory.Release(message);
            StartRecovery();
			return Yield.DidWork.Repeat;
        }
		
		protected override void OnStartRecovery()
		{
			SendStartRealTime();
			EndRecovery();
		}
		
		protected override void ReceiveMessage(MessageMbtQuotes message)
		{
			switch( message.MessageType) {
				case '1':
					Level1Update( message);
					break;
				case '2':
					log.Error( "Message type '2' unknown Message is: " + message);
                    log.Info("Received tick: " + new string(message.DataIn.ReadChars(message.Remaining)));
                    break;
				case '3':
					TimeAndSalesUpdate( message);
					break;
                case '4':
                    OptionChainUpdate( message);
                    break;
                default:
			        var messageInError = new string(message.DataIn.ReadChars(message.Remaining));
                    log.Info("Received tick: " + messageInError);
                    throw new ApplicationException("MBTQuotes message type '" + message.MessageType + "' was unknown: \n" + messageInError);
			}
		}
		
		private unsafe void Level1Update( MessageMbtQuotes message)
		{
		    SymbolHandler handler;
            try
            {
                SymbolInfo symbolInfo = Factory.Symbol.LookupSymbol(message.Symbol);
                handler = symbolHandlers[symbolInfo.BinaryIdentifier];
            }
            catch (ApplicationException)
            {
                log.Info("Received tick: " + new string(message.DataIn.ReadChars(message.Remaining)));
                throw;
            }
			if( message.Bid != 0) {
				handler.Bid = message.Bid;
			}
			if( message.Ask != 0) {
				handler.Ask = message.Ask;
			}
			if( message.AskSize != 0) {
				handler.AskSize = message.AskSize;
			}
			if( message.BidSize != 0) {
				handler.BidSize = message.BidSize;
			}
            UpdateTime(handler,message);
            handler.SendQuote();
            return;
		}

        private void UpdateTime( SymbolHandler handler, MessageMbtQuotes message)
        {
            TimeStamp currentTime;
            if (UseLocalTickTime)
            {
                currentTime = TimeStamp.UtcNow;
            }
            else
            {
                currentTime = new TimeStamp(message.GetTickUtcTime());
            }
            if (currentTime <= handler.Time)
            {
                currentTime.Internal = handler.Time.Internal + 1;
            }
            handler.Time = currentTime;
        }

        private unsafe void OptionChainUpdate(MessageMbtQuotes message)
        {
            var symbol = message.Symbol;
            var symbolInfo = Factory.Symbol.LookupSymbol(symbol);
            var handler = symbolOptionHandlers[symbolInfo.BinaryIdentifier];
            if (message.Bid != 0)
            {
                handler.Bid = message.Bid;
            }
            if (message.Ask != 0)
            {
                handler.Ask = message.Ask;
            }
            if (message.AskSize != 0)
            {
                handler.AskSize = message.AskSize;
            }
            if (message.BidSize != 0)
            {
                handler.BidSize = message.BidSize;
            }
            if( message.Last != 0)
            {
                handler.Last = message.Last;
            }
            if( message.LastSize != 0)
            {
                handler.LastSize = message.LastSize;
            }
            if( message.Strike != 0)
            {
                handler.StrikePrice = message.Strike;
            }
            handler.OptionType = message.OptionType;
            handler.UtcOptionExpiration = new TimeStamp(message.UtcOptionExpiration);
            UpdateTime(handler, message);
            handler.SendOptionPrice();
            handler.Clear();
            return;
        }

        private unsafe void TimeAndSalesUpdate(MessageMbtQuotes message)
        {
			var symbol = message.Symbol;
			var symbolInfo = Factory.Symbol.LookupSymbol(symbol);
			var handler = symbolHandlers[symbolInfo.BinaryIdentifier];
			handler.Last = message.Last;
			if( trace) {
				log.Trace( "Got last trade price: " + handler.Last);// + "\n" + Message);
			}
			handler.LastSize = message.LastSize;
			int condition = message.Condition;
			if( condition != 0 &&
			    condition != 53 &&
			    condition != 45) {
				log.Info( "Trade quote received with non-zero condition: " + condition);
			}
			int status = message.Status;
			if( status != 0) {
				log.Info( "Trade quote received with non-zero status: " + status);
			}
			int type = message.Type;
			if( type != 0) {
				log.Info( "Trade quote received with non-zero type: " + type);
			}
            UpdateTime(handler, message);
            handler.SendTimeAndSales();
            return;
		}
		
		private void OnException( Exception ex) {
			log.Error("Exception occurred", ex);
		}
        
		private void SendStartRealTime() {
			lock( symbolsRequestedLocker) {
				foreach( var kvp in symbolsRequested) {
					SymbolInfo symbol = kvp.Value;
					RequestStartSymbol(symbol);
				}
			}
		}
		
		private void SendEndRealTime() {
			lock( symbolsRequestedLocker) {
				foreach(var kvp in symbolsRequested) {
					SymbolInfo symbol = kvp.Value;
					RequestStopSymbol(symbol);
				}
			}
		}
		
		public override void OnStartSymbol(SymbolInfo symbol)
		{
			if( IsRecovering || IsRecovered) {
				RequestStartSymbol(symbol);
			}
		}
		
		private void RequestStartSymbol(SymbolInfo symbol) {
            StartSymbolHandler(symbol,receiver);
            if( symbol.OptionChain != OptionChain.None)
            {
                StartSymbolOptionHandler(symbol, receiver);
            }
			string quoteType = "";
			switch( symbol.QuoteType) {
				case QuoteType.Level1:
					quoteType = "20000";
					break;
				case QuoteType.Level2:
					quoteType = "20001";
					break;
				case QuoteType.None:
					quoteType = null;
					break;
				default:
					SendError("Unknown QuoteType " + symbol.QuoteType + " for symbol " + symbol + ".");
					return;
			}
			
			string tradeType = "";
			switch( symbol.TimeAndSales) {
				case TimeAndSales.ActualTrades:
					tradeType = "20003";
					break;
				case TimeAndSales.Extrapolated:
					tradeType = null;
					break;
				case TimeAndSales.None:
					tradeType = null;
					break;
				default:
					SendError("Unknown TimeAndSales " + symbol.TimeAndSales + " for symbol " + symbol + ".");
					return;
			}

            string optionChain = "";
            switch (symbol.OptionChain)
            {
                case OptionChain.Complete:
                    optionChain = "20004";
                    break;
                case OptionChain.None:
                    optionChain = null;
                    break;
                default:
                    SendError("Unknown OptionChain " + symbol.OptionChain + " for symbol " + symbol + ".");
                    return;
            }

            if (tradeType != null)
			{
			    Message message = Socket.MessageFactory.Create();
				string textMessage = "S|1003="+symbol.Symbol+";2000="+tradeType+"\n";
				if( debug) log.Debug("Symbol request: " + textMessage);
				message.DataOut.Write(textMessage.ToCharArray());
				while( !Socket.TrySendMessage(message)) {
					if( IsInterrupted) return;
					Factory.Parallel.Yield();
				}
			}
			
			if( quoteType != null)
			{
			    Message message = Socket.MessageFactory.Create();
				string textMessage = "S|1003="+symbol.Symbol+";2000="+quoteType+"\n";
				if( debug) log.Debug("Symbol request: " + textMessage);
				message.DataOut.Write(textMessage.ToCharArray());
				while( !Socket.TrySendMessage(message)) {
					if( IsInterrupted) return;
					Factory.Parallel.Yield();
				}
			}

            if (optionChain != null)
            {
                Message message = Socket.MessageFactory.Create();
                string textMessage = "S|1003=" + symbol.Symbol + ";2000=" + optionChain+ "\n";
                if (debug) log.Debug("Symbol request: " + textMessage);
                message.DataOut.Write(textMessage.ToCharArray());
                while (!Socket.TrySendMessage(message))
                {
                    if (IsInterrupted) return;
                    Factory.Parallel.Yield();
                }
            }

            while (!receiver.OnEvent(symbol, (int)EventType.StartRealTime, null))
            {
            	if( IsInterrupted) return;
            	Factory.Parallel.Yield();
            }
		}
		
		public override void OnStopSymbol(SymbolInfo symbol)
		{
			RequestStopSymbol(symbol);
		}
		
		private void RequestStopSymbol(SymbolInfo symbol) {
       		SymbolHandler buffer = symbolHandlers[symbol.BinaryIdentifier];
       		buffer.Stop();
			receiver.OnEvent(symbol,(int)EventType.EndRealTime,null);
		}
		
		
		
        private void StartSymbolHandler(SymbolInfo symbol, Receiver receiver) {
			lock( symbolHandlersLocker) {
	        	SymbolHandler symbolHandler;
	        	if( symbolHandlers.TryGetValue(symbol.BinaryIdentifier,out symbolHandler)) {
	        		symbolHandler.Start();
	        	} else {
	    	    	symbolHandler = Factory.Utility.SymbolHandler(symbol,receiver);
	    	    	symbolHandlers.Add(symbol.BinaryIdentifier,symbolHandler);
	    	    	symbolHandler.Start();
	        	}
			}
        }

        private void StartSymbolOptionHandler(SymbolInfo symbol, Receiver receiver)
        {
            lock (symbolHandlersLocker)
            {
                SymbolHandler symbolHandler;
                if (symbolOptionHandlers.TryGetValue(symbol.BinaryIdentifier, out symbolHandler))
                {
                    symbolHandler.Start();
                }
                else
                {
                    symbolHandler = Factory.Utility.SymbolHandler(symbol, receiver);
                    symbolOptionHandlers.Add(symbol.BinaryIdentifier, symbolHandler);
                    symbolHandler.Start();
                }
            }
        }
        private object symbolHandlersLocker = new object();
        
		public static string Hash(string password) {
			SHA256 hash = new SHA256Managed();
			char[] chars = password.ToCharArray();
			byte[] bytes = new byte[chars.Length];
			for( int i=0; i<chars.Length; i++) {
				bytes[i] = (byte) chars[i];
			}
			byte[] hashBytes = hash.ComputeHash(bytes);
			string hashString = BitConverter.ToString(hashBytes);
			return hashString.Replace("-","");
		}
		
	}
}
