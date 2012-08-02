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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using TickZoom.Api;
using TickZoom.Provider.FIX;
using TickZoom.Provider.MBTQuotes;

namespace TickZoom.Provider.MBTFIX
{
    public class MBTFIXSimulator : FIXSimulatorSupport {
		private static Log log = Factory.SysLog.GetLogger(typeof(MBTFIXSimulator));
        private volatile bool debug;
        private volatile bool trace;
        public override void RefreshLogLevel()
        {
            base.RefreshLogLevel();
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
        }
        private Random random = new Random(1234);

        public MBTFIXSimulator(string mode, ProjectProperties projectProperties, ProviderSimulatorSupport providerSimulator)
            : base(mode, projectProperties, providerSimulator, 6489, new MessageFactoryFix44())
        {
		    log.Register(this);
		}

		public override void ParseFIXMessage(Message message)
		{
			var packetFIX = (MessageFIX4_4) message;
			switch( packetFIX.MessageType) {
				case "AF": // Request Orders
					FIXOrderList( packetFIX);
					break;
				case "AN": // Request Positions
					FIXPositionList( packetFIX);
					break;
				case "G":
					FIXChangeOrder( packetFIX);
					break;
				case "D":
					FIXCreateOrder( packetFIX);
					break;
				case "F":
					FIXCancelOrder( packetFIX);
					break;
				case "0":
					if( debug) log.DebugFormat(LogMessage.LOGMSG125);
                    ReceivedHeartBeat();
					break;
                case "g":
                    FIXRequestSessionStatus(packetFIX);
                    break;
                case "5":
                    log.Info("Received logout message.");
                    SendLogout();
                    //Dispose();
                    break;
                default: 
					throw new ApplicationException("Unknown FIX message type '" + packetFIX.MessageType + "'\n" + packetFIX);
			}			
		}
		
	    private void FIXOrderList(MessageFIX4_4 packet)
		{
			var mbtMsg = (FIXMessage4_4) FixFactory.Create();
			mbtMsg.SetText("END");
			mbtMsg.AddHeader("8");
            if (debug) log.DebugFormat(LogMessage.LOGMSG126, mbtMsg);
            SendMessage(mbtMsg);
        }

		private void FIXPositionList(MessageFIX4_4 packet)
		{
			var mbtMsg = (FIXMessage4_4) FixFactory.Create();
			mbtMsg.SetText("DONE");
			mbtMsg.AddHeader("AO");
            if (debug) log.DebugFormat(LogMessage.LOGMSG127, mbtMsg);
            SendMessage(mbtMsg);
		}
		
		private void FIXChangeOrder(MessageFIX4_4 packet) {
            var symbol = Factory.Symbol.LookupSymbol(packet.Symbol);
            var order = ConstructOrder(packet, packet.ClientOrderId);
            if (!ProviderSimulator.IsOrderServerOnline)
            {
                log.Info(symbol + ": Rejected " + packet.ClientOrderId + ". Order server offline.");
                OnRejectOrder(order, symbol + ": Order Server Offline.");
                return;
            }
            var simulator = simulators[SimulatorType.RejectSymbol];
            if (FixFactory != null && simulator.CheckFrequencyAndSymbol(symbol))
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG128, packet.MessageType);
                OnRejectOrder(order, "Testing reject of change order.");
                return;
            }
            simulator = simulators[SimulatorType.ServerOfflineReject];
            if (FixFactory != null && simulator.CheckFrequency())
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG129, packet.MessageType);
                OnBusinessRejectOrder(packet.ClientOrderId, "Server offline for change order.");
                ProviderSimulator.SwitchBrokerState("offline", false);
                ProviderSimulator.SetOrderServerOffline();
                return;
            }
            PhysicalOrder origOrder = null;
			if( debug) log.DebugFormat(LogMessage.LOGMSG130, packet.Symbol, packet.ClientOrderId, packet.OriginalClientOrderId);
			try
			{
			    long origClientId;
                if( !long.TryParse(packet.OriginalClientOrderId, out origClientId))
                {
                    log.Error("original client order id " + packet.OriginalClientOrderId + " cannot be converted to long: " + packet);
                    origClientId = 0;
                }
                origOrder = ProviderSimulator.GetOrderById(origClientId);
			} catch( ApplicationException ex) {
				if( debug) log.DebugFormat(LogMessage.LOGMSG131, symbol, packet.ClientOrderId, packet.OriginalClientOrderId, ex.Message);
                OnRejectOrder(order, symbol + ": Cannot change order. Probably already filled or canceled.");
				return;
			}
		    order.OriginalOrder = origOrder;
#if VERIFYSIDE
			if( order.Side != origOrder.Side) {
				var message = symbol + ": Cannot change " + origOrder.Side + " to " + order.Side;
				log.Error( message);
                OnRejectOrder(order, false, message);
				return;     
			}
			if( order.Type != origOrder.Type) {
				var message = symbol + ": Cannot change " + origOrder.Type + " to " + order.Type;
				log.Error( message);
                OnRejectOrder(order, false, message);
				return;     
			}
#endif
            ProviderSimulator.ChangeOrder(order);
            ProcessChangeOrder(order);
		    order.OriginalOrder = null; // Original now gone.
		}

        private void ProcessChangeOrder(PhysicalOrder order)
        {
			SendExecutionReport( order, "E", 0.0, 0, 0, 0, (int) order.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
			SendExecutionReport( order, "5", 0.0, 0, 0, 0, (int) order.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
        }

	    private bool onlineNextTime = false;
        private void FIXRequestSessionStatus(MessageFIX4_4 packet)
        {
            if (packet.TradingSessionId != "TSSTATE")
            {
                throw new ApplicationException("Expected TSSTATE for trading session id but was: " + packet.TradingSessionId);
            }
            if (!packet.TradingSessionRequestId.Contains(sender) || !packet.TradingSessionRequestId.Contains(packet.Sequence.ToString()))
            {
                throw new ApplicationException("Expected unique trading session request id but was:" + packet.TradingSessionRequestId);
            }

            requestSessionStatus = true;
            if (onlineNextTime)
            {
                ProviderSimulator.SetOrderServerOnline();
                onlineNextTime = false;
            }
            if (ProviderSimulator.IsOrderServerOnline)
            {
                SendSessionStatusOnline();
            }
            else
            {
                TrySendSessionStatus("3");
            }
            onlineNextTime = true;
        }


        private void FIXCancelOrder(MessageFIX4_4 packet)
        {
            var symbol = Factory.Symbol.LookupSymbol(packet.Symbol);
            if (!ProviderSimulator.IsOrderServerOnline)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG132, symbol, packet.OriginalClientOrderId);
                OnRejectCancel(packet.Symbol, packet.ClientOrderId, packet.OriginalClientOrderId, symbol + ": Order Server Offline");
                return;
            }
            var simulator = simulators[SimulatorType.RejectSymbol];
            if (FixFactory != null && simulator.CheckFrequencyAndSymbol(symbol))
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG133, packet.MessageType);
                OnRejectCancel(packet.Symbol, packet.ClientOrderId, packet.OriginalClientOrderId, "Testing reject of cancel order.");
                return;
            }
            simulator = simulators[SimulatorType.ServerOfflineReject];
            if (FixFactory != null && simulator.CheckFrequency())
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG129, packet.MessageType);
                OnBusinessRejectOrder(packet.ClientOrderId, "Server offline for cancel order.");
                ProviderSimulator.SwitchBrokerState("offline", false);
                ProviderSimulator.SetOrderServerOffline();
                return;
            }
            if (debug) log.DebugFormat(LogMessage.LOGMSG134, packet.Symbol, packet.OriginalClientOrderId);
            PhysicalOrder origOrder = null;
            try
            {
                long origClientId;
                if (!long.TryParse(packet.OriginalClientOrderId, out origClientId))
                {
                    log.Error("original client order id " + packet.OriginalClientOrderId +
                              " cannot be converted to long: " + packet);
                    origClientId = 0;
                }
                origOrder = ProviderSimulator.GetOrderById(origClientId);
            }
            catch (ApplicationException)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG135, symbol, packet.OriginalClientOrderId);
                OnRejectCancel(packet.Symbol, packet.ClientOrderId, packet.OriginalClientOrderId, "No such order");
                return;
            }
            var cancelOrder = ConstructCancelOrder(packet, packet.ClientOrderId, origOrder);
            ProviderSimulator.CancelOrder(cancelOrder);
            ProcessCancelOrder(cancelOrder);
            ProviderSimulator.TryProcessAdustments(cancelOrder);
            return;
        }

        private void ProcessCancelOrder(PhysicalOrder cancelOrder)
        {
            var origOrder = cancelOrder.OriginalOrder;
		    var randomOrder = random.Next(0, 10) < 5 ? cancelOrder : origOrder;
            SendExecutionReport( randomOrder, "6", 0.0, 0, 0, 0, (int)origOrder.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(cancelOrder.Symbol, ProviderSimulator.GetPosition(cancelOrder.Symbol));
            SendExecutionReport( randomOrder, "4", 0.0, 0, 0, 0, (int)origOrder.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(cancelOrder.Symbol, ProviderSimulator.GetPosition(cancelOrder.Symbol));
		}

        private void FIXCreateOrder(MessageFIX4_4 packet)
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG136, packet.Symbol, packet.ClientOrderId);
            var symbol = Factory.Symbol.LookupSymbol(packet.Symbol);
            var order = ConstructOrder(packet, packet.ClientOrderId);
            if (!ProviderSimulator.IsOrderServerOnline)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG137, symbol, packet.ClientOrderId);
                OnRejectOrder(order, symbol + ": Order Server Offline.");
                return;
            }
            var simulator = simulators[SimulatorType.RejectSymbol];
            if (FixFactory != null && simulator.CheckFrequencyAndSymbol(symbol))
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG128, packet.MessageType);
                OnRejectOrder(order, "Testing reject of create order");
                return;
            }
            simulator = simulators[SimulatorType.ServerOfflineReject];
            if (FixFactory != null && simulator.CheckFrequency())
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG129, packet.MessageType);
                OnBusinessRejectOrder(packet.ClientOrderId, "Server offline for create order.");
                ProviderSimulator.SwitchBrokerState("offline", false);
                ProviderSimulator.SetOrderServerOffline();
                return;
            }
            if (packet.Symbol == "TestPending")
            {
                log.Info("Ignoring FIX order since symbol is " + packet.Symbol);
            }
            else
            {
                if (string.IsNullOrEmpty(packet.ClientOrderId))
                {
                    System.Diagnostics.Debugger.Break();
                }
                ProviderSimulator.CreateOrder(order);
                ProcessCreateOrder(order);
                ProviderSimulator.TryProcessAdustments(order);
            }
            return;
        }

	    private void ProcessCreateOrder(PhysicalOrder order) {
			SendExecutionReport( order, "A", 0.0, 0, 0, 0, (int) order.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
            if( order.Symbol.FixSimulationType == FIXSimulationType.BrokerHeldStopOrder &&
                (order.Type == OrderType.Stop))
            {
                SendExecutionReport(order, "A", "D", 0.0, 0, 0, 0, (int)order.RemainingSize, TimeStamp.UtcNow);
                SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
            }
            else
            {
                SendExecutionReport(order, "0", 0.0, 0, 0, 0, (int)order.RemainingSize, TimeStamp.UtcNow);
                SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
            }
		}
		
		private PhysicalOrder ConstructOrder(MessageFIX4_4 packet, string clientOrderId) {
			if( string.IsNullOrEmpty(clientOrderId)) {
				var message = "Client order id was null or empty. FIX Message is: " + packet;
				log.Error(message);
				throw new ApplicationException(message);
			}
			var symbol = Factory.Symbol.LookupSymbol(packet.Symbol);
			var side = OrderSide.Buy;
			switch( packet.Side) {
				case "1":
					side = OrderSide.Buy;
					break;
				case "2":
					side = OrderSide.Sell;
					break;
				case "5":
					side = OrderSide.SellShort;
					break;
			}
			var type = OrderType.Limit;
			switch( packet.OrderType) {
				case "1":
					type = OrderType.Market;
					break;
				case "2":
					type = OrderType.Limit;
					break;
				case "3":
					type = OrderType.Stop;
					break;
			}
		    long clientId;
			var logicalId = 0;
            if (!long.TryParse(clientOrderId, out clientId))
            {
                log.Error("original client order id " + clientOrderId +
                          " cannot be converted to long: " + packet);
                clientId = 0;
            }
            var utcCreateTime = new TimeStamp(packet.TimeStamp);
		    var physicalOrder = Factory.Utility.PhysicalOrder();
            physicalOrder.Initialize(
				OrderAction.Create, OrderState.Active, symbol, side, type, OrderFlags.None, 
				packet.Price, packet.OrderQuantity, logicalId, 0, clientId, null, utcCreateTime);
			if( debug) log.DebugFormat(LogMessage.LOGMSG138, physicalOrder);
			return physicalOrder;
		}

        private PhysicalOrder ConstructCancelOrder(MessageFIX4_4 packet, string clientOrderId, PhysicalOrder origOrder)
        {
            if (string.IsNullOrEmpty(clientOrderId))
            {
                var message = "Client order id was null or empty. FIX Message is: " + packet;
                log.Error(message);
                throw new ApplicationException(message);
            }
            var symbol = Factory.Symbol.LookupSymbol(packet.Symbol);
            var side = OrderSide.Buy;
            var type = OrderType.None;
            var logicalId = 0;
            long clientId;
            if (!long.TryParse(clientOrderId, out clientId))
            {
                log.Error("original client order id " + clientOrderId +
                          " cannot be converted to long: " + packet);
                clientId = 0;
            }
            var utcCreateTime = new TimeStamp(packet.TimeStamp);
            var physicalOrder = Factory.Utility.PhysicalOrder();
            physicalOrder.Initialize(
                OrderAction.Cancel, OrderState.Active, symbol, side, type, OrderFlags.None,
                0D, 0, logicalId, 0, clientId, null, utcCreateTime);
            physicalOrder.OriginalOrder = origOrder;
            if (debug) log.DebugFormat(LogMessage.LOGMSG138, physicalOrder);
            return physicalOrder;
        }

        protected override FIXTFactory1_1 CreateFIXFactory(int sequence, string target, string sender)
        {
            this.target = target;
            this.sender = sender;
            return new FIXFactory4_4(sequence, target, sender);
        }
		
		private string target;
		private string sender;

        public override void OnRejectOrder(PhysicalOrder order, string error)
		{
			var mbtMsg = (FIXMessage4_4) FixFactory.Create();
			mbtMsg.SetAccount( "33006566");
			mbtMsg.SetClientOrderId( order.BrokerOrder.ToString());
			mbtMsg.SetOrderStatus("8");
			mbtMsg.SetText(error);
            mbtMsg.SetSymbol(order.Symbol.BaseSymbol);
            mbtMsg.SetTransactTime(TimeStamp.UtcNow);
            mbtMsg.AddHeader("8");
            if (trace) log.TraceFormat(LogMessage.LOGMSG139, mbtMsg);
            SendMessage(mbtMsg);
        }

        public override void OnPhysicalFill(PhysicalFill fill, PhysicalOrder order)
        {
            if (order.Symbol.FixSimulationType == FIXSimulationType.BrokerHeldStopOrder &&
                (order.Type == OrderType.Stop))
            {
                order.Type = OrderType.Market;
                var marketOrder = Factory.Utility.PhysicalOrder();
                marketOrder.Initialize(order.Action, order.OrderState,
                    order.Symbol, order.Side, order.Type, OrderFlags.None, 0,
                    order.RemainingSize, order.LogicalOrderId,
                    order.LogicalSerialNumber,
                    order.BrokerOrder, null, TimeStamp.UtcNow);
                SendExecutionReport(marketOrder, "0", 0.0, 0, 0, 0, (int)marketOrder.RemainingSize, TimeStamp.UtcNow);
            }
            if (debug) log.DebugFormat(LogMessage.LOGMSG140, fill);
            SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
            var orderStatus = fill.CumulativeSize == fill.CompleteSize ? "2" : "1";
            SendExecutionReport(order, orderStatus, "F", fill.Price, fill.CompleteSize, fill.CumulativeSize, fill.Size, fill.RemainingSize, fill.UtcTime);
        }

        private void OnRejectCancel(string symbol, string clientOrderId, string origClientOrderId, string error)
        {
            var mbtMsg = (FIXMessage4_4)FixFactory.Create();
            mbtMsg.SetAccount("33006566");
            mbtMsg.SetClientOrderId(clientOrderId);
            mbtMsg.SetOriginalClientOrderId(origClientOrderId);
            mbtMsg.SetOrderStatus("8");
            mbtMsg.SetText(error);
            //mbtMsg.SetSymbol(symbol);
            mbtMsg.SetTransactTime(TimeStamp.UtcNow);
            mbtMsg.AddHeader("9");
            if (trace) log.TraceFormat(LogMessage.LOGMSG141, mbtMsg);
            SendMessage(mbtMsg);
        }

        private void SendPositionUpdate(SymbolInfo symbol, int position)
		{
            //var mbtMsg = (FIXMessage4_4) FixFactory.Create();
            //mbtMsg.SetAccount( "33006566");
            //mbtMsg.SetSymbol( symbol.Symbol);
            //if( position <= 0) {
            //    mbtMsg.SetShortQty( position);
            //} else {
            //    mbtMsg.SetLongQty( position);
            //}
            //mbtMsg.AddHeader("AP");
            //SendMessage(mbtMsg);
            //if(trace) log.Trace("Sending position update: " + mbtMsg);
        }	

		private void SendExecutionReport(PhysicalOrder order, string status, double price, int orderQty, int cumQty, int lastQty, int leavesQty, TimeStamp time)
		{
		    SendExecutionReport(order, status, status, price, orderQty, cumQty, lastQty, leavesQty, time);
		}

	    private void SendExecutionReport(PhysicalOrder order, string status, string executionType, double price, int orderQty, int cumQty, int lastQty, int leavesQty, TimeStamp time) {
			int orderType = 0;
			switch( order.Type) {
				case OrderType.Market:
					orderType = 1;
					break;
				case OrderType.Limit:
					orderType = 2;
					break;
				case OrderType.Stop:
					orderType = 3;
					break;
                case OrderType.None:
			        break;
                default:
                    throw new ApplicationException("Unexpected order type: " + order.Type);
            }
			int orderSide = 0;
			switch( order.Side) {
				case OrderSide.Buy:
					orderSide = 1;
					break;
				case OrderSide.Sell:
					orderSide = 2;
					break;
				case OrderSide.SellShort:
					orderSide = 5;
					break;
                default:
                    throw new ApplicationException("Unexpected order side: " + order.Side);
            }
			var mbtMsg = (FIXMessage4_4) FixFactory.Create();
			mbtMsg.SetAccount( "33006566");
			mbtMsg.SetDestination("MBTX");
			mbtMsg.SetOrderQuantity( orderQty);
			mbtMsg.SetLastQuantity( Math.Abs(lastQty));
			if( lastQty != 0) {
				mbtMsg.SetLastPrice( price);
			}
			mbtMsg.SetCumulativeQuantity( Math.Abs(cumQty));
			mbtMsg.SetOrderStatus(status);
			mbtMsg.SetPositionEffect( "O");
			mbtMsg.SetOrderType( orderType);
			mbtMsg.SetSide( orderSide);
    		mbtMsg.SetClientOrderId( order.BrokerOrder.ToString());
			if( order.OriginalOrder != null) {
				mbtMsg.SetOriginalClientOrderId( order.OriginalOrder.BrokerOrder.ToString());
			}
			mbtMsg.SetPrice( order.Price);
			mbtMsg.SetSymbol( order.Symbol.BaseSymbol);
			mbtMsg.SetTimeInForce( 0);
			mbtMsg.SetExecutionType( executionType);
			mbtMsg.SetTransactTime( time);
			mbtMsg.SetLeavesQuantity( Math.Abs(leavesQty));
			mbtMsg.AddHeader("8");
            SendMessage(mbtMsg);
			if(trace) log.TraceFormat(LogMessage.LOGMSG142, mbtMsg);
		}

        protected override void ResendMessage(FIXTMessage1_1 textMessage)
        {
            var mbtMsg = (FIXMessage4_4) textMessage;
            if( SyncTicks.Enabled && !IsRecovered && mbtMsg.Type == "8")
            {
                switch( mbtMsg.OrderStatus )
                {
                    case "E":
                    case "6":
                    case "A":
                        var symbolInfo = Factory.Symbol.LookupSymbol(mbtMsg.Symbol);
                        var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                        //if (symbolInfo.FixSimulationType == FIXSimulationType.BrokerHeldStopOrder &&
                        //    mbtMsg.ExecutionType == "D")  // restated  
                        //{
                        //    // Ignored order count.
                        //}
                        //else
                        //{
                        //    tickSync.AddPhysicalOrder("resend");
                        //}
                        break;
                    case "2":
                    case "1":
                        symbolInfo = Factory.Symbol.LookupSymbol(mbtMsg.Symbol);
                        tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                        //tickSync.AddPhysicalFill("resend");
                        break;
                }
                
            }
            ResendMessageProtected(textMessage);
        }

        protected override void RemoveTickSync(MessageFIXT1_1 textMessage)
        {
            var mbtMsg = (MessageFIX4_4)textMessage;
            if (SyncTicks.Enabled && mbtMsg.MessageType == "8")
            {
                switch (mbtMsg.OrderStatus)
                {
                    case "E":
                    case "6":
                    case "0":
                        {
                            var symbolInfo = Factory.Symbol.LookupSymbol(mbtMsg.Symbol);
                            var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                            tickSync.RemovePhysicalOrder("offline");
                            if (!tickSync.IsWaitingMatch)
                            {
                                tickSync.AddWaitingMatch("offline");
                            }
                        }
                        break;
                    case "A":
                        if( mbtMsg.ExecutionType == "D")
                        {
                            // Is it a Forex order?
                            var symbolInfo = Factory.Symbol.LookupSymbol(mbtMsg.Symbol);
                            var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                            tickSync.RemovePhysicalOrder("offline");
                            if (!tickSync.IsWaitingMatch)
                            {
                                tickSync.AddWaitingMatch("offline");
                            }
                        }
                        break;
                    case "2":
                    case "1":
                        {
                            var symbolInfo = Factory.Symbol.LookupSymbol(mbtMsg.Symbol);
                            var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                            tickSync.RemovePhysicalFill("offline");
                            if( !tickSync.IsWaitingMatch)
                            {
                                tickSync.AddWaitingMatch("offline");
                            }
                        }
                        break;
                }

            }
        }

        protected override void RemoveTickSync(FIXTMessage1_1 textMessage)
        {
            var mbtMsg = (FIXMessage4_4) textMessage;
            if (SyncTicks.Enabled && mbtMsg.Type == "8")
            {
                switch (mbtMsg.OrderStatus)
                {
                    case "E":
                    case "6":
                    case "0":
                        {
                            var symbolInfo = Factory.Symbol.LookupSymbol(mbtMsg.Symbol);
                            var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                            tickSync.RemovePhysicalOrder("offline");
                        }
                        break;
                    case "A":
                        if (mbtMsg.ExecutionType == "D")
                        {
                            // Is it a Forex order?
                            var symbolInfo = Factory.Symbol.LookupSymbol(mbtMsg.Symbol);
                            var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                            tickSync.RemovePhysicalOrder("offline");
                        }
                        break;
                    case "2":
                    case "1":
                        {
                            var symbolInfo = Factory.Symbol.LookupSymbol(mbtMsg.Symbol);
                            var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                            tickSync.RemovePhysicalFill("offline");
                        }
                        break;
                }

            }
        }
    }
}
