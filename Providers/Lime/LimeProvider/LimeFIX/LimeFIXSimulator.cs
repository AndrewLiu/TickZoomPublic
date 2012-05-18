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
using System.Text;
using System.Threading;

using TickZoom.Api;
using TickZoom.Provider.FIX;
using TickZoom.Provider.LimeQuotes;

namespace TickZoom.Provider.LimeFIX 
{
    public class LimeFIXSimulator : FIXSimulatorSupport {
        private static Log log = Factory.SysLog.GetLogger(typeof(LimeFIXSimulator));
        private volatile bool debug;
        private volatile bool trace;
        public override void RefreshLogLevel()
        {
            base.RefreshLogLevel();
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
        }
        private Random random = new Random(1234);
        
        public LimeFIXSimulator(string mode, ProjectProperties projectProperties, ProviderSimulatorSupport providerSimulator)
            : base(mode, projectProperties, providerSimulator, 6489, new MessageFactoryFix42())
        {
            log.Register(this);
            simulators[SimulatorType.SendServerOffline].Enabled = false;
            simulators[SimulatorType.ReceiveServerOffline].Enabled = false;
            simulators[SimulatorType.ServerOfflineReject].Enabled = false;
        }

        protected override void SetupFixFactory(MessageFIXT1_1 packet) {
            // Lime doesn't allow sequence numbers reset at startup.
            if (debug) log.Debug("Setup Fix Factory");
            if (packet.IsResetSeqNum)
            {
                throw new InvalidOperationException("Found reset sequence number flag is true Lime doesn't support resetting sequence numbers: " + packet);
            }
            if (FixFactory == null)
            {
                fixFactory = CreateFIXFactory(1, packet.Target, packet.Sender);
                if (debug) log.Debug("Set local sequence number to " + fixFactory.LastSequence);
                RemoteSequence = packet.Sequence;
            }
        }

        public override void  SendServerSyncComplete()
        {
            SendHeartbeat();
        }

        private void SendHeartbeat()
        {
            var mbtMsg = FixFactory.Create();
            mbtMsg.AddHeader("0");
            if (trace) log.Trace("Requesting heartbeat: " + mbtMsg);
            SendMessage(mbtMsg);
        }

        public override void ParseFIXMessage(Message message)
        {
            var packetFIX = (MessageFIX4_2)message;
            switch (packetFIX.MessageType)
            {
               case "G":
                    FIXChangeOrder(packetFIX);
                    break;
                case "D":
                    FIXCreateOrder(packetFIX);
                    break;
                case "F":
                    FIXCancelOrder(packetFIX);
                    break;
                case "0":
                    if (debug) log.Debug("Received heartbeat response.");
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
#if NOT_LIME
        private void FIXOrderList(MessageFIX4_2 packet)
        {
            var mbtMsg = (FIXMessage4_4)FixFactory.Create();
            mbtMsg.SetText("END");
            mbtMsg.AddHeader("8");
            if (debug) log.Debug("Sending end of order list: " + mbtMsg);
            SendMessage(mbtMsg);
        }

        private void FIXPositionList(MessageFIX4_2 packet)
        {
            var mbtMsg = (FIXMessage4_4)FixFactory.Create();
            mbtMsg.SetText("DONE");
            mbtMsg.AddHeader("AO");
            if (debug) log.Debug("Sending end of position list: " + mbtMsg);
            SendMessage(mbtMsg);
        }
#endif

        private void FIXChangeOrder(MessageFIX4_2 packet)
        {
            var symbol = Factory.Symbol.LookupSymbol(packet.Symbol);
            var order = ConstructOrder(packet, packet.ClientOrderId);
            if (!ProviderSimulator.IsOrderServerOnline)
            {
                throw new LimeException("Order server offline testing for Lime not yet implemeneted");
                //log.Info(symbol + ": Rejected " + packet.ClientOrderId + ". Order server offline.");
                //OnRejectOrder(order, symbol + ": Order Server Offline.");
                //return;
            }
            var simulator = simulators[SimulatorType.RejectSymbol];
            if (FixFactory != null && simulator.CheckFrequencyAndSymbol(symbol))
            {
                if (debug) log.Debug("Simulating create order reject of 35=" + packet.MessageType);
                OnRejectOrder(order, "Testing reject of change order.");
                return;
            }
            simulator = simulators[SimulatorType.ServerOfflineReject];
            if (FixFactory != null && simulator.CheckFrequency())
            {
                throw new LimeException("Order server offline testing for Lime not yet implemeneted");
                //if (debug) log.Debug("Simulating order server offline business reject of 35=" + packet.MessageType);
                //OnBusinessRejectOrder(packet.ClientOrderId, "Server offline for change order.");
                //ProviderSimulator.SwitchBrokerState("offline", false);
                //ProviderSimulator.SetOrderServerOffline();
                //return;
            }
            CreateOrChangeOrder origOrder = null;
            if (debug) log.Debug("FIXChangeOrder() for " + packet.Symbol + ". Client id: " + packet.ClientOrderId + ". Original client id: " + packet.OriginalClientOrderId);
            try
            {
                long origClientId;
                if (!long.TryParse(packet.OriginalClientOrderId, out origClientId))
                {
                    log.Error("original client order id " + packet.OriginalClientOrderId + " cannot be converted to long: " + packet);
                    origClientId = 0;
                }
                origOrder = ProviderSimulator.GetOrderById(origClientId);
            }
            catch (ApplicationException ex)
            {
                if (debug) log.Debug(symbol + ": Rejected " + packet.ClientOrderId + ". Cannot change order: " + packet.OriginalClientOrderId + ". Already filled or canceled.  Message: " + ex.Message);
                OnRejectOrder(order, symbol + ": Cannot change order. Probably already filled or canceled.");
                return;
            }
            if( packet.OrderQuantity < origOrder.CumulativeSize)
            {
                if (debug) log.Debug(symbol + ": Rejected " + packet.ClientOrderId + ". Order quantity must be greater then cumulative filled quantity: " + origOrder.CumulativeSize);
                OnRejectOrder(order, symbol + ": Order quantity must be greater then cumulative filled quantity.");
                return;
            }
            order.CumulativeSize = origOrder.CumulativeSize;
            order.CompleteSize = packet.OrderQuantity;
            order.RemainingSize = order.CompleteSize - order.CumulativeSize;
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
            order.OriginalOrder = null; // Original is now gone.
        }

        private void ProcessChangeOrder(CreateOrChangeOrder order)
        {
            SendExecutionReport(order, "5", 0.0, 0, 0, 0, (int)order.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
        }

#if NOT_LIME
     private bool onlineNextTime = false;
        private void FIXRequestSessionStatus(MessageFIX4_2 packet)
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
                SendSessionStatus("3");
            }
            onlineNextTime = true;
        }
#endif


        private void FIXCancelOrder(MessageFIX4_2 packet)
        {
            CreateOrChangeOrder origOrder = null;
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
                if (debug) log.Debug("Cannot cancel order by client id: " + packet.OriginalClientOrderId + ". Probably already filled or canceled.");
                OnRejectCancel(packet.Symbol, packet.ClientOrderId, packet.OriginalClientOrderId, "No such order");
                return;
            }
            var symbol = origOrder.Symbol;
            if (debug) log.Debug("FIXCancelOrder() for " + packet.Symbol + ". Original client id: " + packet.OriginalClientOrderId);
            if (!ProviderSimulator.IsOrderServerOnline)
            {
                throw new LimeException("Order server offline testing for Lime not yet implemeneted");
                //if (debug) log.Debug(symbol + ": Cannot cancel order by client id: " + packet.OriginalClientOrderId + ". Order Server Offline.");
                //OnRejectCancel(packet.Symbol, packet.ClientOrderId, packet.OriginalClientOrderId, symbol + ": Order Server Offline");
                //return;
            }
            var simulator = simulators[SimulatorType.RejectSymbol];
            if (FixFactory != null && simulator.CheckFrequencyAndSymbol(symbol))
            {
                if (debug) log.Debug("Simulating cancel order reject of 35=" + packet.MessageType);
                OnRejectCancel(packet.Symbol, packet.ClientOrderId, packet.OriginalClientOrderId, "Testing reject of cancel order.");
                return;
            }
            simulator = simulators[SimulatorType.ServerOfflineReject];
            if (FixFactory != null && simulator.CheckFrequency())
            {
                throw new LimeException("Order server offline testing for Lime not yet implemeneted");
                //if (debug) log.Debug("Simulating order server offline business reject of 35=" + packet.MessageType);
                //OnBusinessRejectOrder(packet.ClientOrderId, "Server offline for cancel order.");
                //ProviderSimulator.SwitchBrokerState("offline", false);
                //ProviderSimulator.SetOrderServerOffline();
                //return;
            }
            var cancelOrder = ConstructCancelOrder(packet, packet.ClientOrderId, origOrder);
            ProviderSimulator.CancelOrder(cancelOrder);
            ProcessCancelOrder(cancelOrder);
            ProviderSimulator.TryProcessAdustments(cancelOrder);
            return;
        }

        private void ProcessCancelOrder(CreateOrChangeOrder cancelOrder)
        {
            var origOrder = cancelOrder.OriginalOrder;
            var randomOrder = random.Next(0, 10) < 5 ? cancelOrder : origOrder;
            SendExecutionReport(randomOrder, "6", 0.0, 0, 0, 0, (int)origOrder.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(cancelOrder.Symbol, ProviderSimulator.GetPosition(cancelOrder.Symbol));
            SendExecutionReport(randomOrder, "4", 0.0, 0, 0, 0, (int)origOrder.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(cancelOrder.Symbol, ProviderSimulator.GetPosition(cancelOrder.Symbol));
        }

        private void FIXCreateOrder(MessageFIX4_2 packet)
        {
            if (debug) log.Debug("FIXCreateOrder() for " + packet.Symbol + ". Client id: " + packet.ClientOrderId);
            var symbol = Factory.Symbol.LookupSymbol(packet.Symbol);
            var order = ConstructOrder(packet, packet.ClientOrderId);
            if (!ProviderSimulator.IsOrderServerOnline)
            {
                if (debug) log.Debug(symbol + ": Rejected " + packet.ClientOrderId + ". Order server offline.");
                OnRejectOrder(order, symbol + ": Order Server Offline.");
                return;
            }
            var simulator = simulators[SimulatorType.RejectSymbol];
            if (FixFactory != null && simulator.CheckFrequencyAndSymbol(symbol))
            {
                if (debug) log.Debug("Simulating create order reject of 35=" + packet.MessageType);
                OnRejectOrder(order, "Testing reject of create order");
                return;
            }
            simulator = simulators[SimulatorType.ServerOfflineReject];
            if (FixFactory != null && simulator.CheckFrequency())
            {
                if (debug) log.Debug("Simulating order server offline business reject of 35=" + packet.MessageType);
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

        private void ProcessCreateOrder(CreateOrChangeOrder order)
        {
            SendExecutionReport(order, "0", 0.0, 0, 0, 0, (int)order.RemainingSize, TimeStamp.UtcNow);
            SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
        }

        private CreateOrChangeOrder ConstructOrder(MessageFIX4_2 packet, string clientOrderId)
        {
            if (string.IsNullOrEmpty(clientOrderId))
            {
                var message = "Client order id was null or empty. FIX Message is: " + packet;
                log.Error(message);
                throw new ApplicationException(message);
            }
            var symbol = Factory.Symbol.LookupSymbol(packet.Symbol);
            var side = OrderSide.Buy;
            switch (packet.Side)
            {
                case "1":
                    side = OrderSide.Buy;
                    break;
                case "2":
                    side = OrderSide.Sell;
                    break;
                case "5":
                case "9":
                    side = OrderSide.SellShort;
                    break;
            }
            var type = OrderType.Limit;
            switch (packet.OrderType)
            {
                case "1":
                    type = OrderType.Market;
                    break;
                case "2":
                    type = OrderType.Limit;
                    break;
                default:
                    //throw new LimeException("Unsupported Order Type");
                    log.Error("Unsupported order type");
                    return null;
            }
      
            long clientId;
            var logicalId = 0;
            if (!long.TryParse(clientOrderId, out clientId))
            {
                log.Error("original client order id " + clientOrderId +
                          " cannot be converted to long: " + packet);
                clientId = 0;
            }
            //var utcCreateTime = new TimeStamp(packet.TransactionTime);
            var utcCreateTime = new TimeStamp(packet.TimeStamp);
            var physicalOrder = Factory.Utility.PhysicalOrder(
                OrderAction.Create, OrderState.Active, symbol, side, type, OrderFlags.None,
                packet.Price, packet.OrderQuantity, logicalId, 0, clientId, null, utcCreateTime);
            if (debug) log.Debug("Received physical Order: " + physicalOrder);
            return physicalOrder;
        }

        private CreateOrChangeOrder ConstructCancelOrder(MessageFIX4_2 packet, string clientOrderId, CreateOrChangeOrder origOrder)
        {
            if (string.IsNullOrEmpty(clientOrderId))
            {
                var message = "Client order id was null or empty. FIX Message is: " + packet;
                log.Error(message);
                throw new ApplicationException(message);
            }
            var symbol = origOrder.Symbol;
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
            var physicalOrder = Factory.Utility.PhysicalOrder(
                OrderAction.Cancel, OrderState.Active, symbol, side, type, OrderFlags.None,
                0D, 0, logicalId, 0, clientId, null, utcCreateTime);
            if (debug) log.Debug("Received physical Order: " + physicalOrder);
            physicalOrder.OriginalOrder = origOrder;
            return physicalOrder;
        }


        protected override FIXTFactory1_1 CreateFIXFactory(int sequence, string target, string sender)
        {
            this.target = target;
            this.sender = sender;
            return new FIXFactory4_2(sequence, target, sender);
        }

        private string target;
        private string sender;

        public override void OnRejectOrder(CreateOrChangeOrder order, string error)
        {
            var mbtMsg = (FIXMessage4_2)FixFactory.Create();
            mbtMsg.SetAccount("33006566");
            mbtMsg.SetClientOrderId(order.BrokerOrder.ToString());
            mbtMsg.SetOrderStatus("8");
            mbtMsg.SetText(error);
            mbtMsg.SetSymbol(order.Symbol.Symbol);
            mbtMsg.SetTransactTime(TimeStamp.UtcNow);
            mbtMsg.AddHeader("8");
            if (trace) log.Trace("Sending reject order: " + mbtMsg);
            SendMessage(mbtMsg);
        }

        public override void OnPhysicalFill(PhysicalFill fill, CreateOrChangeOrder order)
        {
            if (order.Symbol.FixSimulationType == FIXSimulationType.BrokerHeldStopOrder &&
                (order.Type == OrderType.Stop ))
            {
                order.Type = OrderType.Market;
                var marketOrder = Factory.Utility.PhysicalOrder(order.Action, order.OrderState,
                                                                order.Symbol, order.Side, order.Type, OrderFlags.None, 0,
                                                                order.RemainingSize, order.LogicalOrderId,
                                                                order.LogicalSerialNumber,
                                                                order.BrokerOrder, null, TimeStamp.UtcNow);
                SendExecutionReport(marketOrder, "0", 0.0, 0, 0, 0, (int)marketOrder.RemainingSize, TimeStamp.UtcNow);
            }
            if (debug) log.Debug("Converting physical fill to FIX: " + fill);
            SendPositionUpdate(order.Symbol, ProviderSimulator.GetPosition(order.Symbol));
            var orderStatus = fill.CumulativeSize == fill.CompleteSize ? "2" : "1";
            CreateOrChangeOrder origOrder;
            if( ProviderSimulator.TryGetOrderById(order.BrokerOrder, out origOrder))
            {
                origOrder.CompleteSize = Math.Abs(fill.CompleteSize);
                origOrder.CumulativeSize = Math.Abs(fill.CumulativeSize);
                origOrder.RemainingSize = Math.Abs(fill.RemainingSize);
            }
            SendExecutionReport(order, orderStatus, "F", fill.Price, fill.CompleteSize, fill.CumulativeSize, fill.Size, fill.RemainingSize, fill.UtcTime);
        }

        private void OnRejectCancel(string symbol, string clientOrderId, string origClientOrderId, string error)
        {
            var mbtMsg = (FIXMessage4_2)FixFactory.Create();
            mbtMsg.SetAccount("33006566");
            mbtMsg.SetClientOrderId(clientOrderId);
            mbtMsg.SetOriginalClientOrderId(origClientOrderId);
            mbtMsg.SetOrderStatus("8");
            mbtMsg.SetText(error);
            //mbtMsg.SetSymbol(symbol);
            mbtMsg.SetTransactTime(TimeStamp.UtcNow);
            mbtMsg.AddHeader("9");
            if (trace) log.Trace("Sending reject cancel." + mbtMsg);
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

        private void SendExecutionReport(CreateOrChangeOrder order, string status, double price, int orderQty, int cumQty, int lastQty, int leavesQty, TimeStamp time)
        {
            SendExecutionReport(order, status, status, price, orderQty, cumQty, lastQty, leavesQty, time);
        }

        private void SendExecutionReport(CreateOrChangeOrder order, string status, string executionType, double price, int orderQty, int cumQty, int lastQty, int leavesQty, TimeStamp time)
        {
            int orderType = 0;
            switch (order.Type) {
                case OrderType.Market:
                    orderType = 1;
                    break;
                case OrderType.Limit:
                    orderType = 2;
                    break;
                case OrderType.None:
                    break;
                default:
                    throw new LimeException("Unsupported order type: " + order.Type);
            }
            int orderSide = 0;
            switch (order.Side)
            {
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
                    throw new LimeException("Unsupported order side: " + order.Side);
            }
            var mbtMsg = (FIXMessage4_2)FixFactory.Create();
            mbtMsg.SetOrderQuantity(Math.Abs(orderQty));
            mbtMsg.SetLastQuantity(Math.Abs(lastQty));
            if (lastQty != 0)
            {
                mbtMsg.SetLastPrice(price);
            }
            mbtMsg.SetCumulativeQuantity(Math.Abs(cumQty));
            mbtMsg.SetOrderStatus(status);
            mbtMsg.SetPositionEffect("O");
            if( orderType != 0)
            {
                mbtMsg.SetOrderType(orderType);
            }
            if( orderSide > 0)
            {
                mbtMsg.SetSide(orderSide);
            }
            mbtMsg.SetClientOrderId(order.BrokerOrder.ToString());
            if (order.OriginalOrder != null)
            {
                mbtMsg.SetOriginalClientOrderId(order.OriginalOrder.BrokerOrder.ToString());
            }
            mbtMsg.SetPrice(order.Price);
            mbtMsg.SetSymbol(order.Symbol.Symbol);
            mbtMsg.SetTimeInForce(0);
            mbtMsg.SetExecutionType(executionType);
            mbtMsg.SetExcTransType("0");
            mbtMsg.SetTransactTime(time);
            mbtMsg.SetLeavesQuantity(Math.Abs(leavesQty));
            mbtMsg.AddHeader("8");
            SendMessage(mbtMsg);
            if (trace) log.Trace("Sending execution report: " + mbtMsg);
        }

        protected override void ResendMessage(FIXTMessage1_1 textMessage)
        {
            var mbtMsg = (FIXMessage4_2)textMessage;
            if (SyncTicks.Enabled && !IsRecovered && mbtMsg.Type == "8")
            {
                switch (mbtMsg.OrderStatus)
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
            var mbtMsg = (MessageFIX4_2)textMessage;
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

        protected override void RemoveTickSync(FIXTMessage1_1 textMessage)
        {
            var mbtMsg = (FIXMessage4_2)textMessage;
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

