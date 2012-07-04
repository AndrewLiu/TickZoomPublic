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
using System.Text;
using TickZoom.Api;
using TickZoom.Provider.FIX;

namespace TickZoom.Provider.MBTFIX
{
    public class MBTFIXProvider : FIXProviderSupport, LogAware
	{
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(MBTFIXProvider));
		private readonly bool info = log.IsDebugEnabled;
        private volatile bool trace = log.IsTraceEnabled;
        private volatile bool debug = log.IsDebugEnabled;
        private volatile bool verbose = log.IsVerboseEnabled;

        public override void RefreshLogLevel()
        {
            base.RefreshLogLevel();
            if (log != null)
            {
                verbose = log.IsVerboseEnabled;
                debug = log.IsDebugEnabled;
                trace = log.IsTraceEnabled;
            }
        }

        public enum RecoverProgress
        {
            InProgress,
            Completed,
            None,
        }
        private string fixDestination = "MBT";

        private MBTFIXProvider(string name) : base( name)
		{
            log.Register(this);
			log.Notice("Using config section: " + name);
			if( name.Contains(".config")) {
				throw new ApplicationException("Please remove .config from config section name.");
			}
		}

        protected override MessageFactory CreateMessageFactory()
        {
            return new MessageFactoryFix44();
        }

        private void RequestSessionUpdate()
        {
            var mbtMsg = FixFactory.Create();
            mbtMsg.SetTradingSessionRequestId(FixFactory.Sender + "-" + mbtMsg.Sequence + "-" + TimeStamp.UtcNow);
            mbtMsg.SetTradingSessionId("TSSTATE");
            mbtMsg.SetSubscriptionRequestType(1);
            mbtMsg.AddHeader("g");
            if (debug)
            {
                log.DebugFormat("Request Session Update: \n{0}", mbtMsg);
            }
            SendMessage(mbtMsg);
        }

        protected override void SendLogin(int localSequence, bool restartSequence)
        {
            if( restartSequence)
            {
                localSequence = 1;
            }
            else
            {
                localSequence += 500;
            }
            FixFactory = new FIXFactory4_4(localSequence, UserName, fixDestination);
            var mbtMsg = FixFactory.Create();
            mbtMsg.SetEncryption(0);
            mbtMsg.SetHeartBeatInterval(30);
            if( restartSequence)
            {
                mbtMsg.ResetSequence();
            }
            mbtMsg.SetEncoding("554_H1");
            mbtMsg.SetPassword(Password);
            mbtMsg.AddHeader("A");
            if (debug)
            {
                log.DebugFormat("Login message: \n{0}", mbtMsg);
            }
            SendMessage(mbtMsg);
        }

		protected override void OnStartRecovery()
		{
			if( !LogRecovery) {
				MessageFIXT1_1.IsQuietRecovery = true;
			}
		    CancelRecovered();
            TryEndRecovery();
        }

        protected override void OnFinishRecovery()
        {
        }

        protected override void HandleRejectedLogin(MessageFIXT1_1 message)
        {
            // MBT doesn't send any rejected login messages.
        }

		public override void OnStopBroker(SymbolInfo symbol)
		{
            TrySendEndBroker();
        }
	
		private void RequestPositions() {
			var fixMsg = (FIXMessage4_4) FixFactory.Create();
            fixMsg.SetSubscriptionRequestType(1);
            fixMsg.SetAccount(AccountNumber);
			fixMsg.SetPositionRequestId(1);
			fixMsg.SetPositionRequestType(0);
			fixMsg.AddHeader("AN");
			SendMessage(fixMsg);
		}

        protected override void SendHeartbeat()
        {
            if (!isOrderServerOnline) RequestSessionUpdate();
            base.SendHeartbeat();
        }

        private unsafe bool VerifyLoginAck(MessageFIXT1_1 message)
		{
		    var packetFIX = message;
		    if ("A" == packetFIX.MessageType &&
		        "FIX.4.4" == packetFIX.Version &&
		        "MBT" == packetFIX.Sender &&
		        UserName == packetFIX.Target &&
		        "0" == packetFIX.Encryption)
		    {
                return true;
            }
            else
		    {
                var textMessage = new StringBuilder();
                textMessage.AppendLine("Invalid login response:");
                textMessage.AppendLine("  message type = " + packetFIX.MessageType);
                textMessage.AppendLine("  version = " + packetFIX.Version);
                textMessage.AppendLine("  sender = " + packetFIX.Sender);
                textMessage.AppendLine("  target = " + packetFIX.Target);
                textMessage.AppendLine("  encryption = " + packetFIX.Encryption);
                textMessage.AppendLine("  sequence = " + packetFIX.Sequence);
                textMessage.AppendLine("  heartbeat interval = " + packetFIX.HeartBeatInterval);
                textMessage.AppendLine(packetFIX.ToString());
                log.Error(textMessage.ToString());
                return false;
            }
		}

        protected override bool CheckForServerSync(MessageFIXT1_1 messageFix)
        {
            return messageFix.MessageType == "h" || messageFix.MessageType == "1";
        }

        protected override bool HandleLogon(MessageFIXT1_1 message)
        {
            if (ConnectionStatus != Status.PendingLogin)
            {
                throw new InvalidOperationException("Attempt logon when in " + ConnectionStatus +
                                                    " instead of expected " + Status.PendingLogin);
            }
            if (VerifyLoginAck(message))
            {
                return true;
            }
            else
            {
                SocketReconnect.Regenerate();
                return false;
            }
        }
		
		protected override void ReceiveMessage(Message message) {
			var packetFIX = (MessageFIX4_4) message;
			switch( packetFIX.MessageType) {
                case "h":
                    SessionStatus(packetFIX);
			        break;
                case "AP":
				case "AO":
					PositionUpdate( packetFIX);
					break;
				case "8":
                    if( string.IsNullOrEmpty(packetFIX.TransactionTime))
                    {
                        throw new ApplicationException("Found FIX message with empty transaction time: " + packetFIX);
                    }
                    var transactTime = new TimeStamp(packetFIX.TransactionTime);
                    if( transactTime >= OrderStore.LastSequenceReset)
                    {
                        ExecutionReport(packetFIX);
                    }
                    else
                    {
                        if( debug) log.DebugFormat("Ignoring execution report of sequence {0} because transact time {1} is earlier than last sequence reset {2}", packetFIX.Sequence, transactTime, OrderStore.LastSequenceReset);
                    }
					break;
				case "9":
					CancelRejected( packetFIX);
                    break;
				case "1":
                    if (debug) log.DebugFormat("Received Test Request");
					SendHeartbeat();
					break;
				case "0":
                    if (debug) log.DebugFormat("Received Heartbeat");
					break;
				case "j":
                    BusinessReject(packetFIX);
                    break;
				default:
					log.Warn("Ignoring Message: '" + packetFIX.MessageType + "'\n" + packetFIX);
					break;
			}
		}

        protected override void BusinessReject(MessageFIXT1_1 packetFIX)
        {
            var text = packetFIX.Text;
            var lower = text.ToLower();

            var errorOkay = false;
            errorOkay = lower.Contains("server") ? true : errorOkay;
            errorOkay = text.Contains("DEMOORDS") ? true : errorOkay;
            errorOkay = text.Contains("FXORD1") ? true : errorOkay;
            errorOkay = text.Contains("FXORD2") ? true : errorOkay;
            errorOkay = text.Contains("FXORD01") ? true : errorOkay;
            errorOkay = text.Contains("FXORD02") ? true : errorOkay;
            if (errorOkay)
            {
                isOrderServerOnline = false;
            }

            HandleBusinessReject(errorOkay, packetFIX);
        }

        protected override void TryEndRecovery()
        {
            if (debug) log.DebugFormat("TryEndRecovery Status {0}, Session Status Online {1}, Resend Complete {2}", ConnectionStatus, isOrderServerOnline, IsResendComplete);
            switch (ConnectionStatus)
            {
                case Status.Recovered:
                case Status.PendingLogOut:
                case Status.PendingLogin:
                case Status.PendingServerResend:
                case Status.Disconnected:
                    return;
                case Status.PendingRecovery:
                    if (IsResendComplete && isOrderServerOnline)
                    {
                        OrderStore.RequestSnapshot();
                        EndRecovery();
                        RequestPositions();
                        RequestSessionUpdate();
                        StartPositionSync();
                        return;
                    }
                    break;
                default:
                    throw new ApplicationException("Unexpected connection status for TryEndRecovery: " + ConnectionStatus);
            }
        }

        private Dictionary<string,bool> sessionStatusMap = new Dictionary<string, bool>();

        private void SessionStatus(MessageFIX4_4 packetFIX)
        {
            var newIsSessionStatusOnline = false;
            log.DebugFormat("Found session status for {0} or {1}: {2}", packetFIX.TradingSessionId, packetFIX.TradingSessionSubId, packetFIX.TradingSessionStatus);
            var subId = string.IsNullOrEmpty(packetFIX.TradingSessionSubId)
                            ? packetFIX.TradingSessionId
                            : packetFIX.TradingSessionSubId;
            if( !CompareSession( subId) )
            {
                return;
            }
            switch (packetFIX.TradingSessionStatus)
            {
                case 2:
                    sessionStatusMap[subId] = true;
                    newIsSessionStatusOnline = true;
                    break;
                case 3:
                    sessionStatusMap[subId] = false;
                    break;
                default:
                    log.Warn("Received unknown server session status: " + packetFIX.TradingSessionStatus);
                    break;
            }
            foreach (var status in sessionStatusMap)
            {
                if (!status.Value)
                {
                    newIsSessionStatusOnline = false;
                }
            }
            if( debug) log.DebugFormat("Order server connected (new {0}, previous {1}", newIsSessionStatusOnline, isOrderServerOnline);
            if (newIsSessionStatusOnline != isOrderServerOnline)
            {
                isOrderServerOnline = newIsSessionStatusOnline;
                if (isOrderServerOnline)
                {
                    CancelRecovered();
                    TrySendEndBroker();
                    TryEndRecovery();
                }
                else
                {
                    var message = "Order server went offline. Attempting to reconnect.";
                    if( SyncTicks.Enabled)
                    {
                        log.Notice(message);
                    } else
                    {
                        log.Error(message);
                    }
                    CancelRecovered();
                    TrySendEndBroker();
                    TryEndRecovery();
                }
            }
            else if( trace)
            {
                var message = "Order server continues offline. Attempting to reconnect.";
                log.TraceFormat(message);
            }
	    }

        private void PositionUpdate( MessageFIX4_4 packetFIX) {
			if( packetFIX.MessageType == "AO") {
				if(debug) log.DebugFormat("PositionUpdate Complete.");
                TryEndRecovery();
			}
            else 
            {
                var position = packetFIX.LongQuantity + packetFIX.ShortQuantity;
                SymbolInfo symbolInfo;
                if( !Factory.Symbol.TryLookupSymbol(packetFIX.Symbol, out symbolInfo)) {
                    log.Warn("Unable to find " + packetFIX.Symbol + " for position update.");
                    return;
                }
                var algorithm = algorithms.CreateAlgorithm(symbolInfo);
                if (debug) log.DebugFormat("PositionUpdate for {0}: MBT actual ={1}, TZ actual={2}", symbolInfo, position, algorithm.OrderAlgorithm.ActualPosition);
            }
		}

		private void ExecutionReport( MessageFIX4_4 packetFIX)
		{
		    var clientOrderId = 0L;
		    long.TryParse(packetFIX.ClientOrderId, out clientOrderId);
            var originalClientOrderId = 0L;
		    long.TryParse(packetFIX.OriginalClientOrderId, out originalClientOrderId);
            if (packetFIX.Text == "END")
            {
                throw new ApplicationException("Unexpected END in FIX Text field. Never sent a 35=AF message.");
            }
            SymbolAlgorithm algorithm = null;
            SymbolInfo symbolInfo;
            if (!Factory.Symbol.TryLookupSymbol(packetFIX.Symbol, out symbolInfo))
            {
                log.Warn("Unable to find " + packetFIX.Symbol + " for execution report.");
                return;
            }
            if( symbolInfo != null)
            {
                algorithm = algorithms.CreateAlgorithm(symbolInfo);
            }

            string orderStatus = packetFIX.OrderStatus;
            switch (orderStatus)
            {
                case "0": // New
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport New: {0}", packetFIX);
                    }
                    if (algorithm == null)
                    {
                        log.Info("New order but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                        break;
                    }
                    PhysicalOrder order = null;
                    OrderStore.TryGetOrderById(clientOrderId, out order);
                    if (order != null && symbolInfo.FixSimulationType == FIXSimulationType.BrokerHeldStopOrder) // Stop Order
                    {
                        if( order.Type == OrderType.Stop)
                        {
                            if (debug) log.DebugFormat("New order message for Forex Stop: {0}", packetFIX);
                            break;
                        }
                    }
                    algorithm.OrderAlgorithm.ConfirmCreate(clientOrderId, IsRecovered);
                    TrySendStartBroker(symbolInfo, "sync on confirm cancel");
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "1": // Partial
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Partial: {0}", packetFIX);
                    }
                    //UpdateOrder(packetFIX, OrderState.Active, null);
                    SendFill(packetFIX);
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "2":  // Filled 
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Filled: {0}", packetFIX);
                    }
                    if (packetFIX.CumulativeQuantity < packetFIX.LastQuantity)
                    {
                        log.Warn("Ignoring message due to CumQty " + packetFIX.CumulativeQuantity + " less than " + packetFIX.LastQuantity + ". This is a workaround for a MBT FIX server which sends an extra invalid fill message on occasion.");
                        break;
                    }
                    SendFill(packetFIX);
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "5": // Replaced
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Replaced: {0}", packetFIX);
                    }
                    if (algorithm == null)
                    {
                        log.Info("ConfirmChange but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                        break;
                    }
                    algorithm.OrderAlgorithm.ConfirmChange(clientOrderId, IsRecovered);
                    TrySendStartBroker(symbolInfo, "sync on confirm change");
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "4": // Canceled
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Canceled: {0}", packetFIX);
                    }
                    if (algorithm == null)
                    {
                        log.Info("Order Canceled but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                        break;
                    }
                    if (originalClientOrderId > 0)
                    {
                        algorithm.OrderAlgorithm.ConfirmCancel(originalClientOrderId, IsRecovered);
                    }
                    else
                    {
                        algorithm.OrderAlgorithm.ConfirmCancel(clientOrderId, IsRecovered);
                    }
                    TrySendStartBroker(symbolInfo, "sync on confirm cancel orig order");
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "6": // Pending Cancel
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Pending Cancel: {0}", packetFIX);
                    }
                    if (!string.IsNullOrEmpty(packetFIX.Text) && packetFIX.Text.Contains("multifunction order"))
                    {
                        if (debug && (LogRecovery || IsRecovered))
                        {
                            log.DebugFormat("Pending cancel of multifunction order, so removing {0} and {1}", packetFIX.ClientOrderId, packetFIX.OriginalClientOrderId);
                        }
                        if( clientOrderId != 0L) OrderStore.RemoveOrder(clientOrderId);
                        if (originalClientOrderId != 0L) OrderStore.RemoveOrder(originalClientOrderId);
                        break;
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    TryHandlePiggyBackFill(packetFIX);
                    break;
                case "8": // Rejected
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Reject: {0}", packetFIX);
                    }
                    RejectOrder(packetFIX);
                    break;
                case "9": // Suspended
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Suspended: {0}", packetFIX);
                    }
                    RejectOrder(packetFIX);
                    break;
                case "A": // PendingNew
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Pending New: {0}", packetFIX);
                    }
                    if (algorithm == null)
                    {
                        log.Info("PendingNew but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                        break;
                    }

                    OrderStore.TryGetOrderById(clientOrderId, out order);
                    if (order != null && symbolInfo.FixSimulationType == FIXSimulationType.BrokerHeldStopOrder &&
                        (order.Type == OrderType.Stop))
                    {
                        if( packetFIX.ExecutionType == "D")  // Restated
                        {
                            if (debug) log.DebugFormat("Ignoring restated message 150=D for Forex stop execution report 39=A.");
                        }
                        else
                        {
                            algorithm.OrderAlgorithm.ConfirmCreate(clientOrderId, IsRecovered);
                        }
                    }
                    else
                    {
                        algorithm.OrderAlgorithm.ConfirmActive(clientOrderId, IsRecovered);
                    }
                    TrySendStartBroker(symbolInfo, "sync on confirm cancel orig order");
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "E": // Pending Replace
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Pending Replace: {0}", packetFIX);
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    TryHandlePiggyBackFill(packetFIX);
                    break;
                case "R": // Resumed.
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat("ExecutionReport Resumed: {0}", packetFIX);
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    //UpdateOrder(packetFIX, OrderState.Active, null);
                    // Ignore
                    break;
                default:
                    throw new ApplicationException("Unknown order status: '" + orderStatus + "'");
            }
		}

		private void TryHandlePiggyBackFill(MessageFIX4_4 packetFIX) {
			if( packetFIX.LastQuantity > 0) {
                if (debug) log.DebugFormat("TryHandlePiggyBackFill triggering fill because LastQuantity = {0}", packetFIX.LastQuantity);
                SendFill(packetFIX);
            }
		}

        private void CancelRejected(MessageFIX4_4 packetFIX)
        {
            if (debug) log.DebugFormat("CancelRejected: {0}", packetFIX);
            if (packetFIX.Text.Contains("Order Server Offline") ||
                packetFIX.Text.Contains("Trading temporarily unavailable") ||
                packetFIX.Text.Contains("Order Server Not Available"))
            {
                CancelRecovered();
                TrySendEndBroker();
                TryEndRecovery();
            }

            HandleCancelReject(packetFIX.ClientOrderId, packetFIX.Text, packetFIX);
        }

        public void SendFill( MessageFIX4_4 packetFIX) {
            var clientOrderId = 0L;
            long.TryParse(packetFIX.ClientOrderId, out clientOrderId);
            var originalClientOrderId = 0L;
            long.TryParse(packetFIX.ClientOrderId, out originalClientOrderId);
            if (debug) log.DebugFormat("SendFill( {0})", packetFIX.ClientOrderId);
            SymbolInfo symbolInfo;
            if (!Factory.Symbol.TryLookupSymbol(packetFIX.Symbol, out symbolInfo))
            {
                log.Warn("Unable to find " + packetFIX.Symbol + " for fill.");
                return;
            }
            var timeZone = new SymbolTimeZone(symbolInfo);
            var algorithm = algorithms.CreateAlgorithm(symbolInfo);
            var fillPosition = packetFIX.LastQuantity * SideToSign(packetFIX.Side);
            if (symbolReceivers.GetSymbolStatus(symbolInfo))
            {
                PhysicalOrder order;
                if( OrderStore.TryGetOrderById( clientOrderId, out order)) {
				    TimeStamp executionTime;
				    if( UseLocalFillTime) {
					    executionTime = TimeStamp.UtcNow;
				    } else {
					    executionTime = new TimeStamp(packetFIX.TransactionTime);
				    }
				    var configTime = executionTime;
				    configTime.AddSeconds( timeZone.UtcOffset(executionTime));
                    var fill = Factory.Utility.PhysicalFill(symbolInfo, fillPosition, packetFIX.LastPrice, configTime, executionTime, order.BrokerOrder, false, packetFIX.OrderQuantity, packetFIX.CumulativeQuantity, packetFIX.LeavesQuantity, IsRecovered, true);
				    if( debug) log.DebugFormat( "Sending physical fill: {0}", fill);
                    algorithm.OrderAlgorithm.ProcessFill(fill);
                    algorithm.OrderAlgorithm.ProcessOrders();
                    TrySendStartBroker(symbolInfo, "position sync on fill");
                }
                else
                {
                    algorithm.OrderAlgorithm.IncreaseActualPosition(fillPosition);
                    log.Notice("Fill id " + packetFIX.ClientOrderId + " not found. Must have been a manual trade.");
                    if( SyncTicks.Enabled)
                    {
                        var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                        tickSync.RemovePhysicalFill(packetFIX.ClientOrderId);
                    }
                }
			}
        }

		public void RejectOrder( MessageFIX4_4 packetFIX)
		{
		    if (packetFIX.Text.Contains("Order Server Offline") ||
                packetFIX.Text.Contains("Trading temporarily unavailable") ||
                packetFIX.Text.Contains("Order Server Not Available"))
            {
                CancelRecovered();
                TrySendEndBroker();
                TryEndRecovery();
            }

		    HandleOrderReject(packetFIX.ClientOrderId, packetFIX.Symbol, packetFIX.Text, packetFIX);
		}

        public override bool OnCreateBrokerOrder(PhysicalOrder physicalOrder)
		{
            if (!IsRecovered) return false;
			if( debug) log.DebugFormat( "OnCreateBrokerOrder {0}. Connection {1}, IsOrderServerOnline {2}", physicalOrder, ConnectionStatus, isOrderServerOnline);
            if( physicalOrder.Action != OrderAction.Create)
            {
                throw new InvalidOperationException("Expected action Create but was " + physicalOrder.Action);
            }
            OnCreateOrChangeBrokerOrder(physicalOrder, false);
		    return true;
		}
	        
		private void OnCreateOrChangeBrokerOrder(PhysicalOrder order, bool resend)
		{
            var fixMsg = (FIXMessage4_4)(resend ? FixFactory.Create(order.Sequence) : FixFactory.Create());
            order.Sequence = fixMsg.Sequence;
            OrderStore.SetOrder(order);
            OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);

            if (order.RemainingSize > order.Symbol.MaxOrderSize)
            {
                throw new ApplicationException("Order was greater than MaxOrderSize of " + order.Symbol.MaxPositionSize + " for:\n" + order);
            }

            var orderHandler = algorithms.CreateAlgorithm(order.Symbol);
		    var orderSize = order.Side == OrderSide.Buy ? order.RemainingSize : -order.RemainingSize;
            if (Math.Abs(orderHandler.OrderAlgorithm.ActualPosition + orderSize) > order.Symbol.MaxPositionSize)
            {
                throw new ApplicationException("Order was greater than MaxPositionSize of " + order.Symbol.MaxPositionSize + " for:\n" + order);
            }

			if( debug) log.DebugFormat( "Adding Order to open order list: {0}", order);
			if( order.Action == OrderAction.Change) {
                var origBrokerOrder = order.OriginalOrder.BrokerOrder;
                fixMsg.SetClientOrderId(order.BrokerOrder.ToString());
				fixMsg.SetOriginalClientOrderId(origBrokerOrder.ToString());
			    PhysicalOrder origOrder;
				if( OrderStore.TryGetOrderById(origBrokerOrder, out origOrder))
				{
                    origOrder.ReplacedBy = order;
				    if (debug) log.DebugFormat("Setting replace property of {0} to {1}", origBrokerOrder, order.BrokerOrder);
                }
			} else {
				fixMsg.SetClientOrderId(order.BrokerOrder.ToString());
			}
			fixMsg.SetAccount(AccountNumber);
            if (order.Action == OrderAction.Change)
            {
				fixMsg.AddHeader("G");
			} else {
				fixMsg.AddHeader("D");
				if( order.Symbol.Destination.ToLower() == "default") {
					fixMsg.SetDestination("MBTX");
				} else {
					fixMsg.SetDestination(order.Symbol.Destination);
				}
			}
			fixMsg.SetHandlingInstructions(1);
			fixMsg.SetSymbol(order.Symbol.BaseSymbol);
			fixMsg.SetSide( GetOrderSide(order.Side));
			switch( order.Type) {
				case OrderType.Limit:
					fixMsg.SetOrderType(2);
					fixMsg.SetPrice(order.Price);
                    switch( order.Symbol.TimeInForce)
                    {
                        case TimeInForce.Day:
                            fixMsg.SetTimeInForce(0);
                            break;
                        case TimeInForce.GTC:
                            fixMsg.SetTimeInForce(1);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
					break;
                case OrderType.Stop:
					fixMsg.SetOrderType(3);
					fixMsg.SetPrice(order.Price);
					fixMsg.SetStopPrice(order.Price);
                    switch (order.Symbol.TimeInForce)
                    {
                        case TimeInForce.Day:
                            fixMsg.SetTimeInForce(0);
                            break;
                        case TimeInForce.GTC:
                            fixMsg.SetTimeInForce(1);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                case OrderType.Market:
                    fixMsg.SetOrderType(1);
                    fixMsg.SetTimeInForce(0);
                    break;
            }
			fixMsg.SetLocateRequired("N");
			fixMsg.SetSendTime(order.UtcCreateTime);
			fixMsg.SetOrderQuantity(order.RemainingSize);
			fixMsg.SetOrderCapacity("A");
			fixMsg.SetUserName();
            if (order.Action == OrderAction.Change)
            {
				if( verbose) log.VerboseFormat("Change order: \n{0}", fixMsg);
			} else {
                if (verbose) log.VerboseFormat("Create new order: \n{0}", fixMsg);
			}
            if( resend)
            {
                fixMsg.SetDuplicate(true);
            }
			SendMessage( fixMsg);
		}

        protected override void ResendOrder(PhysicalOrder order)
        {
            if( order.Action == OrderAction.Cancel)
            {
                if (debug) log.DebugFormat("Resending cancel order: {0}", order);
                SendCancelOrder(order, true);
            }
            else
            {
                if (debug) log.DebugFormat("Resending order: {0}", order);
                OnCreateOrChangeBrokerOrder(order, true);
            }
        }

		public override bool OnCancelBrokerOrder(PhysicalOrder order)
		{
            if (!IsRecovered) return false;
            if (debug) log.DebugFormat("OnCancelBrokerOrder {0}. Connection {1}, IsOrderServerOnline {2}", order, ConnectionStatus, isOrderServerOnline);
            OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
            PhysicalOrder physicalOrder;
			try {
                physicalOrder = OrderStore.GetOrderById(order.OriginalOrder.BrokerOrder);
			} catch( ApplicationException ex) {
                if (LogRecovery || !IsRecovery)
                {
                    log.Info("Order probably already canceled. " + ex.Message);
                }
			    if( SyncTicks.Enabled) {
					var tickSync = SyncTicks.GetTickSync(order.Symbol.BinaryIdentifier);
					tickSync.RemovePhysicalOrder();
				}
				return true;
			}
            physicalOrder.ReplacedBy = order;
		    if( !object.ReferenceEquals(order.OriginalOrder,physicalOrder))
            {
                throw new ApplicationException("Different objects!");
            }

            SendCancelOrder(order, false);
            return true;

		}

        private void SendCancelOrder( PhysicalOrder order, bool resend)
        {
            var fixMsg = (FIXMessage4_4)(resend ? FixFactory.Create(order.Sequence) : FixFactory.Create());
            order.Sequence = fixMsg.Sequence;
            OrderStore.SetOrder(order);
            var newClientOrderId = order.BrokerOrder;
            fixMsg.SetOriginalClientOrderId(order.OriginalOrder.BrokerOrder.ToString());
            fixMsg.SetClientOrderId(newClientOrderId.ToString());
            fixMsg.SetAccount(AccountNumber);
            fixMsg.SetSide(GetOrderSide(order.OriginalOrder.Side));
            fixMsg.AddHeader("F");
            fixMsg.SetSymbol(order.Symbol.BaseSymbol);
            fixMsg.SetSendTime(order.OriginalOrder.UtcCreateTime);
            if (resend)
            {
                fixMsg.SetDuplicate(true);
            }
            SendMessage(fixMsg);
        }
		
		public override bool OnChangeBrokerOrder(PhysicalOrder physicalOrder)
		{
            if (!IsRecovered) return false;
            if (debug) log.DebugFormat("OnChangeBrokerOrder( {0}. Connection {1}, IsOrderServerOnline {2}", physicalOrder, ConnectionStatus, isOrderServerOnline);
            if (physicalOrder.Action != OrderAction.Change)
            {
                throw new InvalidOperationException("Expected action Change but was " + physicalOrder.Action);
            }
            OnCreateOrChangeBrokerOrder(physicalOrder, false);
            return true;
		}

	}
}