#region Copyright
/*
 * Software: TickZoom Trading Platform
 * Copyright 2012 M. Wayne Walter
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
using System.Text.RegularExpressions;
using TickZoom.Api;
using TickZoom.FIX;

namespace TickZoom.LimeFIX
{
    public class LimeFIXProvider : FIXProviderSupport, PhysicalOrderHandler
    {
        private static readonly Log log = Factory.SysLog.GetLogger(typeof(LimeFIXProvider));
		private readonly bool info = log.IsDebugEnabled;
        private volatile bool trace = log.IsTraceEnabled;
        private volatile bool debug = log.IsDebugEnabled;
        private volatile bool verbose = log.IsVerboseEnabled;
        Dictionary<int, int> physicalToLogicalOrderMap = new Dictionary<int, int>();
        private string fixDestination = "LIME";

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


        public LimeFIXProvider(string name)
            : base(name)
        {
            log.Register(this);
            log.Notice("Using config section: " + name);
			if( name.Contains(".config")) {
                throw new ApplicationException("Please remove .config from config section name.");
            }
        }

        public override void PositionChange(PositionChangeDetail positionChange)
        {
            for( var current = positionChange.Orders.First; current != null; current = current.Next)
            {
                var order = current.Value;
                switch (order.Type)
                {
                    case OrderType.Stop:
                        order.IsSynthetic = true;
                        break;
                }
            }
            base.PositionChange(positionChange);
        }

		public override void OnRetry() {
		}

        protected override MessageFactory CreateMessageFactory()
        {
            return new MessageFactoryFix42();
        }

        public int ProcessOrders()
        {
            return 0;
        }

        public bool IsChanged
        {
            get { return false; }
            set { }
        }

        #region Login

        protected override void SendLogin(int localSequence, bool restartSequence)
        {
            // Ignore restartSequence because Lime doesn't support resetting with 141=Y.
            FixFactory = new FIXFactory4_2(localSequence + 1, UserName, fixDestination);
            var loginMessage = FixFactory.Create() as FIXMessage4_2;
            loginMessage.SetEncryption(0);
            loginMessage.SetHeartBeatInterval(30);
            loginMessage.SetUserName(AccountNumber);
            loginMessage.SetPassword(Password);
            loginMessage.AddHeader("A");
            if (debug)
            {
                log.Debug("Login message: \n" + loginMessage);
            }
            SendMessage(loginMessage);
        }

        #region Logout

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

        public override void OnStopSymbol(SymbolInfo symbol)
		{
            TrySendEndBroker();
        }
	
		private void RequestPositions() {
			var fixMsg = (FIXMessage4_2) FixFactory.Create();
            fixMsg.SetSubscriptionRequestType(1);
            fixMsg.SetAccount(AccountNumber);
			fixMsg.SetPositionRequestId(1);
			fixMsg.SetPositionRequestType(0);
			fixMsg.AddHeader("AN");
			SendMessage(fixMsg);
		}

        protected override void HandleRejectedLogin(MessageFIXT1_1 message)
        {
            bool handled = false;
            var message42 = (MessageFIX4_2)message;
            if (message42.Text != null)
            {
                // If our sequences numbers don't match, Lime sends a logout with a message 
                // telling us what we should be at.  So if we can, we just use that when we reconnect.
                if (message42.Text.StartsWith("MsgSeqNum too low"))
                {
                    var match = Regex.Match(message42.Text, "expecting (\\d+)");
                    int newSequenceNumber = 0;
                    if (match.Success && int.TryParse(match.Groups[1].Value, out newSequenceNumber) && newSequenceNumber >= OrderStore.LocalSequence)
                    {
                        log.Error(message42.Text);
                        OrderStore.SetSequences(OrderStore.RemoteSequence, newSequenceNumber);
                        Socket.Dispose();
                        handled = true;
                        RetryStart = 2;
                        fastRetry = true;
                    }
                }
            }
        }
        #endregion

        private unsafe bool VerifyLoginAck(MessageFIXT1_1 message)
        {
            var packetFIX = message;
            if ("A" == packetFIX.MessageType &&
                "FIX.4.2" == packetFIX.Version &&
                "LIME" == packetFIX.Sender &&
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
            return messageFix.MessageType == "0" || messageFix.MessageType == "1";
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
                RegenerateSocket();
                return false;
            }
        }

        #endregion
      
		protected override void ReceiveMessage(Message message) {
			var packetFIX = (MessageFIX4_2) message;
			switch( packetFIX.MessageType) {
                case "8":
                    if (trace) log.Trace("Received Execution Report");
                    var transactTime = new TimeStamp(packetFIX.TransactionTime);
                    if (transactTime >= OrderStore.LastSequenceReset)
                    {
                        ExecutionReport(packetFIX);
                    }
                    else
                    {
                        if (debug) log.Debug("Ignoring execution report of sequence " + packetFIX.Sequence + " because transact time " + transactTime + " is earlier than last sequence reset " + OrderStore.LastSequenceReset);
                    }
                    break;
                case "9":
                    if (trace) log.Trace("Received Cancel Rejected");
                    CancelRejected(packetFIX);
                    break;
                case "j":
                    if (trace) log.Trace("Received Business Reject");
                    BusinessReject(packetFIX);
                    break;
                case "0":
                    if (trace) log.Trace("Received Hearbeat");
			        SetOrderServerOnline();
                    break;
                case "1":
                    if (trace) log.Trace("Received Test Request");
                    SendHeartbeat();
                    break;
                default:
                    log.Error("Ignoring Message: '" + packetFIX.MessageType + "'\n" + packetFIX);
                    break;
            }
        }

        private void SetOrderServerOnline() {
            if ( !isOrderServerOnline )
                log.Notice("Lime Order Server now Online");
            isOrderServerOnline = true;
            if (debug) log.Debug("Setting Order Server online");
            CancelRecovered();
            TrySendEndBroker();
            TryEndRecovery();
        }

        private void BusinessReject(MessageFIX4_2 packetFIX) {
            HandleBusinessReject(false, packetFIX);
        }

        protected override void TryEndRecovery()
        {
            if (debug) log.Debug("TryEndRecovery Status " + ConnectionStatus +
                ", Session Status Online " + isOrderServerOnline +
                ", Resend Complete " + IsResendComplete);
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
                        //RequestPositions();
                        //RequestSessionUpdate();
                        StartPositionSync();
                        return;
                    }
                    break;
                default:
                    throw new ApplicationException("Unexpected connection status for TryEndRecovery: " + ConnectionStatus);
            }
        }

        private void ExecutionReport( MessageFIX4_2 packetFIX)
		{
		    var clientOrderId = 0L;
		    long.TryParse(packetFIX.ClientOrderId, out clientOrderId);
            var originalClientOrderId = 0L;
		    long.TryParse(packetFIX.ClientOrderId, out originalClientOrderId);
            if (packetFIX.Text == "END")
            {
                throw new ApplicationException("Unexpected END in FIX Text field. Never sent a 35=AF message.");
            }
            SymbolAlgorithm algorithm = null;
		    SymbolInfo symbolInfo = packetFIX.Symbol != null ? Factory.Symbol.LookupSymbol(packetFIX.Symbol) : null;
            if( symbolInfo != null)
            {
                TryGetAlgorithm(symbolInfo.BinaryIdentifier, out algorithm);
            }

            string orderStatus = packetFIX.OrderStatus;
            switch (orderStatus)
            {
                case "0": // New
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.Debug("ExecutionReport New: " + packetFIX);
                    }
                    if (algorithm == null)
                    {
                        log.Info("New order but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                        break;
                    }
                    CreateOrChangeOrder order = null;
                    OrderStore.TryGetOrderById(clientOrderId, out order);
                    if (order != null && symbolInfo.FixSimulationType == FIXSimulationType.BrokerHeldStopOrder) // Stop Order
                    {
                        if( order.Type == OrderType.Stop)
                        {
                            if (debug) log.Debug("New order message for Forex Stop: " + packetFIX);
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
                        log.Debug("ExecutionReport Partial: " + packetFIX);
                    }
                    //UpdateOrder(packetFIX, OrderState.Active, null);
                    SendFill(packetFIX);
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "2":  // Filled 
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.Debug("ExecutionReport Filled: " + packetFIX);
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
                        log.Debug("ExecutionReport Replaced: " + packetFIX);
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
                        log.Debug("ExecutionReport Canceled: " + packetFIX);
                    }
                    if (algorithm == null)
                    {
                        log.Info("Order Canceled but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                        break;
                    }
                    if (clientOrderId != 0)
                    {
                        algorithm.OrderAlgorithm.ConfirmCancel(clientOrderId, IsRecovered);
                        TrySendStartBroker(symbolInfo, "sync on confirm cancel");
                    }
                    else if (originalClientOrderId != 0)
                    {
                        algorithm.OrderAlgorithm.ConfirmCancel(originalClientOrderId, IsRecovered);
                        TrySendStartBroker(symbolInfo, "sync on confirm cancel orig order");
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "6": // Pending Cancel
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.Debug("ExecutionReport Pending Cancel: " + packetFIX);
                    }
                    if (!string.IsNullOrEmpty(packetFIX.Text) && packetFIX.Text.Contains("multifunction order"))
                    {
                        if (debug && (LogRecovery || IsRecovered))
                        {
                            log.Debug("Pending cancel of multifunction order, so removing " + packetFIX.ClientOrderId + " and " + packetFIX.OriginalClientOrderId);
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
                        log.Debug("ExecutionReport Reject: " + packetFIX);
                    }
                    RejectOrder(packetFIX);
                    break;
                case "9": // Suspended
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.Debug("ExecutionReport Suspended: " + packetFIX);
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "A": // PendingNew
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.Debug("ExecutionReport Pending New: " + packetFIX);
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
                            if (debug) log.Debug("Ignoring restated message 150=D for Forex stop execution report 39=A.");
                        }
                        else
                        {
                            algorithm.OrderAlgorithm.ConfirmCreate(originalClientOrderId, IsRecovered);
                        }
                    }
                    else
                    {
                        algorithm.OrderAlgorithm.ConfirmActive(originalClientOrderId, IsRecovered);
                    }
                    TrySendStartBroker(symbolInfo, "sync on confirm cancel orig order");
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "E": // Pending Replace
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.Debug("ExecutionReport Pending Replace: " + packetFIX);
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    TryHandlePiggyBackFill(packetFIX);
                    break;
                case "R": // Resumed.
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.Debug("ExecutionReport Resumed: " + packetFIX);
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    //UpdateOrder(packetFIX, OrderState.Active, null);
                    // Ignore
                    break;
                default:
                    throw new ApplicationException("Unknown order status: '" + orderStatus + "'");
            }
		}

		private void TryHandlePiggyBackFill(MessageFIX4_2 packetFIX) {
			if( packetFIX.LastQuantity > 0) {
                if (debug) log.Debug("TryHandlePiggyBackFill triggering fill because LastQuantity = " + packetFIX.LastQuantity);
                SendFill(packetFIX);
            }
        }

        private void CancelRejected(MessageFIX4_2 packetFIX)
        {
            if (debug) log.Debug("CancelRejected: " + packetFIX);
            HandleCancelReject(packetFIX.ClientOrderId, packetFIX.Text, packetFIX);
        }


        public void SendFill(MessageFIX4_2 packetFIX) {
            var clientOrderId = 0L;
            long.TryParse(packetFIX.ClientOrderId, out clientOrderId);
            var originalClientOrderId = 0L;
            long.TryParse(packetFIX.ClientOrderId, out originalClientOrderId);
            if (debug) log.Debug("SendFill( " + packetFIX.ClientOrderId + ")");
            var symbolInfo = Factory.Symbol.LookupSymbol(packetFIX.Symbol);
            var timeZone = new SymbolTimeZone(symbolInfo);
            SymbolAlgorithm algorithm;
            if (!TryGetAlgorithm(symbolInfo.BinaryIdentifier, out algorithm))
            {
                log.Info("Fill received but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                return;
            }
            var fillPosition = packetFIX.LastQuantity * SideToSign(packetFIX.Side);
            if (GetSymbolStatus(symbolInfo))
            {
                CreateOrChangeOrder order;
                if( OrderStore.TryGetOrderById( clientOrderId, out order)) {
                    TimeStamp executionTime;
				    if( UseLocalFillTime) {
                        executionTime = TimeStamp.UtcNow;
				    } else {
                        executionTime = new TimeStamp(packetFIX.TransactionTime);
                    }
                    var configTime = executionTime;
                    configTime.AddSeconds(timeZone.UtcOffset(executionTime));
                    var fill = Factory.Utility.PhysicalFill(symbolInfo, fillPosition, packetFIX.LastPrice, configTime, executionTime, order.BrokerOrder, false, packetFIX.OrderQuantity, packetFIX.CumulativeQuantity, packetFIX.LeavesQuantity, IsRecovered, true);
                    if (debug) log.Debug("Sending physical fill: " + fill);
                    algorithm.OrderAlgorithm.ProcessFill(fill);
                    algorithm.OrderAlgorithm.ProcessOrders();
                    TrySendStartBroker(symbolInfo, "position sync on fill");
                }
                else
                {
                    algorithm.OrderAlgorithm.IncreaseActualPosition(fillPosition);
                    log.Notice("Fill id " + packetFIX.ClientOrderId + " not found. Must have been a manual trade.");
                    if (SyncTicks.Enabled)
                    {
                        var tickSync = SyncTicks.GetTickSync(symbolInfo.BinaryIdentifier);
                        tickSync.RemovePhysicalFill(packetFIX.ClientOrderId);
                    }
                }
            }
        }

        public void RejectOrder(MessageFIX4_2 packetFIX)
        {
            HandleOrderReject(packetFIX.ClientOrderId, packetFIX.Symbol, packetFIX.Text, packetFIX);
        }

        public override bool OnCreateBrokerOrder(CreateOrChangeOrder createOrChangeOrder)
        {
            if (!IsRecovered) return false;
            if (debug) log.Debug("OnCreateBrokerOrder " + createOrChangeOrder + ". Connection " + ConnectionStatus + ", IsOrderServerOnline " + isOrderServerOnline);
            if (createOrChangeOrder.Action != OrderAction.Create)
            {
                throw new InvalidOperationException("Expected action Create but was " + createOrChangeOrder.Action);
            }
            OnCreateOrChangeBrokerOrder(createOrChangeOrder, false);
            return true;
        }

        private void OnCreateOrChangeBrokerOrder(CreateOrChangeOrder order, bool resend)
        {
            var fixMsg = (FIXMessage4_2)(resend ? FixFactory.Create(order.Sequence) : FixFactory.Create());
            order.Sequence = fixMsg.Sequence;
            OrderStore.SetOrder(order);
            OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);

            if (order.Size > order.Symbol.MaxOrderSize)
            {
                throw new ApplicationException("Order was greater than MaxOrderSize of " + order.Symbol.MaxPositionSize + " for:\n" + order);
            }

            var orderHandler = GetAlgorithm(order.Symbol.BinaryIdentifier);
            var orderSize = order.Side == OrderSide.Sell ? -order.Size : order.Size;
            if (Math.Abs(orderHandler.OrderAlgorithm.ActualPosition + orderSize) > order.Symbol.MaxPositionSize)
            {
                throw new ApplicationException("Order was greater than MaxPositionSize of " + order.Symbol.MaxPositionSize + " for:\n" + order);
            }

            if (debug) log.Debug("Adding Order to open order list: " + order);
			if( order.Action == OrderAction.Change) {
                var origBrokerOrder = order.OriginalOrder.BrokerOrder;
                fixMsg.SetClientOrderId(order.BrokerOrder.ToString());
				fixMsg.SetOriginalClientOrderId(origBrokerOrder.ToString());
                CreateOrChangeOrder origOrder;
                if (OrderStore.TryGetOrderById(origBrokerOrder, out origOrder))
                {
                    origOrder.ReplacedBy = order;
                    if (debug) log.Debug("Setting replace property of " + origBrokerOrder + " to " + order.BrokerOrder);
                }
			} else {
				fixMsg.SetClientOrderId(order.BrokerOrder.ToString());
            }

            if (order.Action == OrderAction.Change)
            {
                fixMsg.AddHeader("G");
			} else {
                fixMsg.AddHeader("D");
				if( order.Symbol.Destination.ToLower() == "default") {
                    fixMsg.SetDestination(Destination);
				} else {
                    fixMsg.SetDestination(order.Symbol.Destination);
                }
            }
            fixMsg.SetSymbol(order.Symbol.Symbol);
            fixMsg.SetSide(order.Side == OrderSide.Buy ? 1 : 5);
			switch( order.Type) {
                case OrderType.Limit:
                    fixMsg.SetOrderType(2);
                    fixMsg.SetPrice(order.Price);
                    fixMsg.SetTimeInForce(0); // Lime only supports Day orders.
                    break;
                case OrderType.Market:
                    fixMsg.SetOrderType(1);
                    //fixMsg.SetTimeInForce(0);
                    break;
                case OrderType.Stop:
                   // throw new LimeException("Lime does not accept Buy Stop Orders");
                    log.Error("Lime: Stops not supproted");
                    break;
                default:
                    throw new LimeException("Unknown OrderType");
            }
            fixMsg.SetOrderQuantity((int)order.Size);
            if (order.Action == OrderAction.Change)
            {
                if (verbose) log.Verbose("Change order: \n" + fixMsg);
            }
            else
            {
                if (verbose) log.Verbose("Create new order: \n" + fixMsg);
            }
            if (resend)
            {
                fixMsg.SetDuplicate(true);
            }
            fixMsg.SetSendTime(order.UtcCreateTime);
            SendMessage(fixMsg);
        }

		private long GetUniqueOrderId() {
            return TimeStamp.UtcNow.Internal;
        }

        protected override void ResendOrder(CreateOrChangeOrder order)
        {
            if (order.Action == OrderAction.Cancel)
            {
                if (debug) log.Debug("Resending cancel order: " + order);
                //if (SyncTicks.Enabled && !IsRecovered)
                //{
                //    TryAddPhysicalOrder(order);
                //}
                SendCancelOrder(order, true);
            }
            else
            {
                if (debug) log.Debug("Resending order: " + order);
                //if (SyncTicks.Enabled && !IsRecovered)
                //{
                //    TryAddPhysicalOrder(order);
                //}
                OnCreateOrChangeBrokerOrder(order, true);
            }
        }

        public override bool OnCancelBrokerOrder(CreateOrChangeOrder order)
        {
            if (!IsRecovered) return false;
            if (debug) log.Debug("OnCancelBrokerOrder " + order + ". Connection " + ConnectionStatus + ", IsOrderServerOnline " + isOrderServerOnline);
            OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
            CreateOrChangeOrder createOrChangeOrder;
			try {
                createOrChangeOrder = OrderStore.GetOrderById(order.OriginalOrder.BrokerOrder);
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
            createOrChangeOrder.ReplacedBy = order;
            if (!object.ReferenceEquals(order.OriginalOrder, createOrChangeOrder))
            {
                throw new ApplicationException("Different objects!");
            }

            SendCancelOrder(order, false);
            return true;

        }

        private void TryAddPhysicalOrder(CreateOrChangeOrder order)
        {
            var tickSync = SyncTicks.GetTickSync(order.Symbol.BinaryIdentifier);
            tickSync.AddPhysicalOrder(order);
        }

        private void SendCancelOrder(CreateOrChangeOrder order, bool resend)
        {
            var fixMsg = (FIXMessage4_2)(resend ? FixFactory.Create(order.Sequence) : FixFactory.Create());
            order.Sequence = fixMsg.Sequence;
            OrderStore.SetOrder(order);
            var newClientOrderId = order.BrokerOrder;
            fixMsg.SetOriginalClientOrderId(order.OriginalOrder.BrokerOrder.ToString());
            fixMsg.SetClientOrderId(newClientOrderId.ToString());
            fixMsg.AddHeader("F");
            if (resend)
            {
                fixMsg.SetDuplicate(true);
            }
            SendMessage(fixMsg);
        }

        public override  bool OnChangeBrokerOrder(CreateOrChangeOrder createOrChangeOrder)
        {
            if (!IsRecovered) return false;
            if (debug) log.Debug("OnChangeBrokerOrder( " + createOrChangeOrder + ". Connection " + ConnectionStatus + ", IsOrderServerOnline " + isOrderServerOnline);
            if (createOrChangeOrder.Action != OrderAction.Change)
            {
                throw new InvalidOperationException("Expected action Change but was " + createOrChangeOrder.Action);
            }
            OnCreateOrChangeBrokerOrder(createOrChangeOrder, false);
            return true;
        }

    }
}
