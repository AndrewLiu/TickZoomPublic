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
using System.Security.Cryptography;
using System.Text;
using TickZoom.Api;
using TickZoom.Provider.FIX;

namespace TickZoom.Provider.MBTQuotes
{
	public class MBTQuotesProvider : QuotesProviderSupport
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

	    private MBTQuotesProvider(string name) : base( name, new MessageFactoryMbtQuotes())
		{
		    log.Register(this);
			if( name.Contains(".config")) {
				throw new ApplicationException("Please remove .config from config section name.");
			}
			RetryStart = 1;
			RetryIncrease = 1;
			RetryMaximum = 30;
            if( System.Diagnostics.Debugger.IsAttached && SyncTicks.Enabled)
            {
                HeartbeatDelay = int.MaxValue;
            }
            else
            {
	  			HeartbeatDelay = 5;
			}
		    log.Info("Ping timeout is " + HeartbeatDelay + " seconds.");
		}
		
		
		public override void OnDisconnect()
		{
		}
		
		public override void OnRetry()
		{
		}
        
		public override void SendLogin()
		{
		    Socket.MessageFactory = new MessageFactoryMbtQuotes();
		    Message message = Socket.MessageFactory.Create();
		    string hashPassword = Hash(Password);
		    string login = "L|100=" + UserName + ";133=" + hashPassword + "\n";
		    if (trace) log.TraceFormat(LogMessage.LOGMSG149, login);
		    if (debug) log.DebugFormat(LogMessage.LOGMSG149, login);
		    message.DataOut.Write(login.ToCharArray());
		    while (!Socket.TrySendMessage(message))
		    {
		        if (IsInterrupted) return;
		        Factory.Parallel.Yield();
		    }
		}

        public override bool VerifyLogin()
        {
            Message message;
		    if( !Socket.TryGetMessage(out message)) return false;
            var disconnect = message as DisconnectMessage;
            if( disconnect != null)
            {
                OnDisconnect(disconnect.Socket);
                return false;
            }
	        message.BeforeRead();
            var loginResponse = new string(message.DataIn.ReadChars(message.Remaining));
            var firstChar = loginResponse[0];
	        if( firstChar == 'G')
	        {
	            log.Info("MBT Quotes API Login response: " + loginResponse);
	            return true;
	        }
			if( debug) log.DebugFormat(LogMessage.LOGMSG150, loginResponse);
			return false;
        }
		
		protected override void OnStartRecovery()
		{
			SendStartRealTime();
			EndRecovery();
		}
		
		protected void ReceiveMessage(MessageMbtQuotes message)
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
                    if( trace)
                    {
                        message.Data.Position = 0;
			            var messageText = new string(message.DataIn.ReadChars(message.Remaining));
                        log.TraceFormat(LogMessage.LOGMSG151, messageText);
                    }
                    // Filter Form T trades which are belatedly entered at the last close price
                    // later on the next day with sales type 30031 or condition 29.
                    // Also eliminate condition 53 which is an average trade price for the day.
                    if( message.SalesType != 30031 && message.Condition != 29 && message.Condition != 53)
                    {
                        TimeAndSalesUpdate(message);
                    }
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

	    protected override bool SendPing()
        {
            Message message = Socket.MessageFactory.Create();
            string textMessage = "9|\n";
            if (trace) log.TraceFormat(LogMessage.LOGMSG152, textMessage);
            message.DataOut.Write(textMessage.ToCharArray());
            while (!Socket.TrySendMessage(message))
            {
                if (IsInterrupted) return false;
                Factory.Parallel.Yield();
            }
	        return true;
        }

        protected override void ProcessSocketMessage(Message rawMessage)
        {
            var message = (MessageMbtQuotes)rawMessage;
            message.BeforeRead();
            if (trace)
            {
                log.TraceFormat(LogMessage.LOGMSG151, new string(message.DataIn.ReadChars(message.Remaining)));
            }
            if (message.MessageType == '9')
            {
                // Received the ping response.
                if (trace) log.TraceFormat(LogMessage.LOGMSG153);
                ReceivedPing();
                SendStartRealTime();
            }
            else
            {
                try
                {
                    ReceiveMessage(message);
                }
                catch (Exception ex)
                {
                    var loggingString = Encoding.ASCII.GetString(message.Data.GetBuffer(), 0, (int)message.Data.Length);
                    log.Error("Unable to process this message:\n" + loggingString, ex);
                }
            }
        }

        protected override ConfigFile LoadProperties(string configFilePath)
        {
            var configFile = base.LoadProperties(configFilePath);
            configFile.AssureValue("EquityDemo/UseLocalTickTime", "true");
            configFile.AssureValue("EquityDemo/ServerAddress", "216.52.236.111");
            configFile.AssureValue("EquityDemo/ServerPort", "5020");
            configFile.AssureValue("EquityDemo/UserName", "CHANGEME");
            configFile.AssureValue("EquityDemo/Password", "CHANGEME");
            configFile.AssureValue("ForexDemo/UseLocalTickTime", "true");
            configFile.AssureValue("ForexDemo/ServerAddress", "216.52.236.111");
            configFile.AssureValue("ForexDemo/ServerPort", "5020");
            configFile.AssureValue("ForexDemo/UserName", "CHANGEME");
            configFile.AssureValue("ForexDemo/Password", "CHANGEME");
            configFile.AssureValue("EquityLive/UseLocalTickTime", "true");
            configFile.AssureValue("EquityLive/ServerAddress", "216.52.236.129");
            configFile.AssureValue("EquityLive/ServerPort", "5020");
            configFile.AssureValue("EquityLive/UserName", "CHANGEME");
            configFile.AssureValue("EquityLive/Password", "CHANGEME");
            configFile.AssureValue("ForexLive/UseLocalTickTime", "true");
            configFile.AssureValue("ForexLive/ServerAddress", "216.52.236.129");
            configFile.AssureValue("ForexLive/ServerPort", "5020");
            configFile.AssureValue("ForexLive/UserName", "CHANGEME");
            configFile.AssureValue("ForexLive/Password", "CHANGEME");
            configFile.AssureValue("Simulate/UseLocalTickTime", "false");
            configFile.AssureValue("Simulate/ServerAddress", "127.0.0.1");
            configFile.AssureValue("Simulate/ServerPort", "6488");
            configFile.AssureValue("Simulate/UserName", "simulate1");
            configFile.AssureValue("Simulate/Password", "only4sim");
            return configFile;
        }

        private void Level1Update(MessageMbtQuotes message)
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

        private void OptionChainUpdate(MessageMbtQuotes message)
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

        private void TimeAndSalesUpdate(MessageMbtQuotes message)
        {
			var symbol = message.Symbol;
			var symbolInfo = Factory.Symbol.LookupSymbol(symbol);
			var handler = symbolHandlers[symbolInfo.BinaryIdentifier];
			handler.Last = message.Last;
			if( trace) {
				log.TraceFormat(LogMessage.LOGMSG154, handler.Last);// + "\n" + Message);
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
		
		protected void SendStartRealTime() {
			lock( symbolsRequestedLocker) {
				foreach( var kvp in symbolsRequested) {
					var symbol = kvp.Value;
					RequestStartSymbol(symbol.Symbol,symbol.Agent);
				}
			}
		}
		
		private void SendEndRealTime() {
			lock( symbolsRequestedLocker) {
				foreach(var kvp in symbolsRequested) {
					var symbol = kvp.Value;
					RequestStopSymbol(symbol.Symbol,symbol.Agent);
				}
			}
		}
		
		public override void OnStartSymbol(SymbolInfo symbol, Agent symbolAgent)
		{
			if( IsRecovering || IsRecovered) {
				RequestStartSymbol(symbol, symbolAgent);
			}
		}
		
		private void RequestStartSymbol(SymbolInfo symbol, Agent symbolAgent) {
            StartSymbolHandler(symbol,symbolAgent);
            if( symbol.OptionChain != OptionChain.None)
            {
                StartSymbolOptionHandler(symbol, symbolAgent);
            }
			string quoteType = "";
			switch( symbol.CaptureQuoteType) {
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
					SendError("Unknown CaptureQuoteType " + symbol.CaptureQuoteType + " for symbol " + symbol + ".");
					return;
			}
			
			string tradeType = "";
			switch( symbol.CaptureTimeAndSales) {
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
					SendError("Unknown CaptureTimeAndSales " + symbol.CaptureTimeAndSales + " for symbol " + symbol + ".");
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
				string textMessage = "S|1003="+symbol.BaseSymbol+";2000="+tradeType+"\n";
				if( debug) log.DebugFormat(LogMessage.LOGMSG155, textMessage);
				message.DataOut.Write(textMessage.ToCharArray());
				while( !Socket.TrySendMessage(message)) {
					if( IsInterrupted) return;
					Factory.Parallel.Yield();
				}
			}
			
			if( quoteType != null)
			{
			    Message message = Socket.MessageFactory.Create();
				string textMessage = "S|1003="+symbol.BaseSymbol+";2000="+quoteType+"\n";
				if( debug) log.DebugFormat(LogMessage.LOGMSG155, textMessage);
				message.DataOut.Write(textMessage.ToCharArray());
				while( !Socket.TrySendMessage(message)) {
					if( IsInterrupted) return;
					Factory.Parallel.Yield();
				}
			}

            if (optionChain != null)
            {
                Message message = Socket.MessageFactory.Create();
                string textMessage = "S|1003=" + symbol.BaseSymbol + ";2000=" + optionChain+ "\n";
                if (debug) log.DebugFormat(LogMessage.LOGMSG155, textMessage);
                message.DataOut.Write(textMessage.ToCharArray());
                while (!Socket.TrySendMessage(message))
                {
                    if (IsInterrupted) return;
                    Factory.Parallel.Yield();
                }
            }

		    var item = new EventItem(symbol, EventType.StartRealTime);
		    symbolAgent.SendEvent(item);
		}
		
		public override void OnStopSymbol(SymbolInfo symbol, Agent agent)
		{
			RequestStopSymbol(symbol,agent);
		}
		
		private void RequestStopSymbol(SymbolInfo symbol, Agent symbolAgent) {
       		SymbolHandler handler = symbolHandlers[symbol.BinaryIdentifier];
       		handler.Stop();
            var item = new EventItem(symbol, EventType.EndRealTime);
            symbolAgent.SendEvent(item);
		}


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

        protected override void Dispose(bool disposing)
        {
            if( debug)
            {
                foreach (var handler in symbolHandlers)
                {
                    log.DebugFormat(LogMessage.LOGMSG156, handler.Value.Symbol, handler.Value.TickCount);
                }
            }
            base.Dispose(disposing);
        }
	}
}
