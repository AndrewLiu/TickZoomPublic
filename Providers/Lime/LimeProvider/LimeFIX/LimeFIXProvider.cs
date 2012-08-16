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
using System.Text.RegularExpressions;
using TickZoom.Api;
using TickZoom.Provider.FIX;

namespace TickZoom.Provider.LimeFIX
{
    public class LimeFIXProvider : FIXProviderSupport, PhysicalOrderHandler
    {
        private Log log = Factory.SysLog.GetLogger(typeof(LimeFIXProvider));
        private volatile bool trace;
        private volatile bool debug;
        private volatile bool verbose;
        private Dictionary<int, int> physicalToLogicalOrderMap = new Dictionary<int, int>();
        private string fixDestination = "LIME";
        private string algorithmName;

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
            log = Factory.SysLog.GetLogger(typeof(LimeFIXProvider) + "." + name);
            log.Register(this);
            log.Notice("Using config section: " + name);
			if( name.Contains(".config")) {
                throw new ApplicationException("Please remove .config from config section name.");
            }
            // Lime only support market and limit orders so force stops to get treated
            // synthetically as market orders when the price gets touched.
            algorithms.ForceSyntheticStops = true;
        }

        protected override MessageFactory CreateMessageFactory()
        {
            return new MessageFactoryFix42();
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
                log.DebugFormat(LogMessage.LOGMSG90, loginMessage);
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

        public override void OnStopBroker(SymbolInfo symbol)
		{
            TrySendEndBroker();
        }
	
        protected override void HandleRejectedLogin(MessageFIXT1_1 message)
        {
            var result = true;
            var message42 = (MessageFIX4_2)message;
            if (message42.Text != null)
            {
                // If our sequences numbers don't match, Lime sends a logout with a message 
                // telling us what we should be at.  So if we can, we just use that when we reconnect.
                if (message42.Text.StartsWith("MsgSeqNum too low"))
                {
                    var match = Regex.Match(message42.Text, "but received (\\d+)");
                    int badSequenceNumber = 0;
                    if (match.Success && int.TryParse(match.Groups[1].Value, out badSequenceNumber))
                    {
                        FixFactory.RollbackLastLogin();
                    }
                    else
                    {
                        result = false;
                    }

                    if( result)
                    {
                        match = Regex.Match(message42.Text, "expecting (\\d+)");
                        int newSequenceNumber = 0;
                        if (match.Success && int.TryParse(match.Groups[1].Value, out newSequenceNumber) && newSequenceNumber >= OrderStore.LocalSequence)
                        {
                            log.Error(message42.Text);
                            var remoteSequence = OrderStore.RemoteSequence == 0 ? message.Sequence + 1 : OrderStore.RemoteSequence;
                            var highestSequence = OrderStore.GetHighestSequence();
                            if (highestSequence > newSequenceNumber)
                            {
                                newSequenceNumber = highestSequence + 1;
                            }
                            OrderStore.SetSequences(remoteSequence, newSequenceNumber);
                            FixFactory.SetNextSequence(newSequenceNumber);
                            OrderStore.TrySnapshot();
                            SocketReconnect.FastRetry();
                        }
                        else
                        {
                            result = false;
                        }
                    }
                }

            }
            if( !result)
            {
                throw new ApplicationException("Unable to parse rejected login error message: " + message42.Text);
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
                SocketReconnect.Regenerate();
                return false;
            }
        }

        #endregion
      
		protected override void ReceiveMessage(Message message) {
			var packetFIX = (MessageFIX4_2) message;
			switch( packetFIX.MessageType) {
                case "8":
                    if (trace) log.TraceFormat(LogMessage.LOGMSG159);
                    var transactTime = new TimeStamp(packetFIX.TransactionTime);
                    if (transactTime >= OrderStore.LastSequenceReset)
                    {
                        ExecutionReport(packetFIX);
                        if (debug) log.DebugFormat(LogMessage.LOGMSG163);
                        if (debug) OrderStore.LogOrders(log);
                    }
                    else
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG91, packetFIX.Sequence, transactTime, OrderStore.LastSequenceReset);
                    }
                    break;
                case "9":
                    if (trace) log.TraceFormat(LogMessage.LOGMSG160);
                    CancelRejected(packetFIX);
                    break;
                case "j":
                    if (trace) log.TraceFormat(LogMessage.LOGMSG161);
                    BusinessReject(packetFIX);
                    break;
                case "0":
                    if (debug) log.DebugFormat(LogMessage.LOGMSG93);
			        TrySetOrderServerOnline();
                    break;
                case "1":
                    if (debug) log.DebugFormat(LogMessage.LOGMSG92);
                    TrySetOrderServerOnline();
                    SendHeartbeat();
                    break;
                default:
                    log.Error("Ignoring Message: '" + packetFIX.MessageType + "'\n" + packetFIX);
                    break;
            }
        }

        private void TrySetOrderServerOnline() {
            if ( !isOrderServerOnline )
            {
                log.Notice("Lime Order Server now Online");
                isOrderServerOnline = true;
                if (debug) log.DebugFormat(LogMessage.LOGMSG162);
                CancelRecovered();
                TrySendEndBroker();
                TryEndRecovery();
            }
        }

        protected override void TryEndRecovery()
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG94, ConnectionStatus, isOrderServerOnline, IsResendComplete);
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
                        EndRecovery();
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
		    long.TryParse(packetFIX.OriginalClientOrderId, out originalClientOrderId);
            if (packetFIX.Text == "END")
            {
                throw new ApplicationException("Unexpected END in FIX Text field. Never sent a 35=AF message.");
            }
            SymbolAlgorithm algorithm = null;
            SymbolInfo symbolInfo;
            var symbolString = string.IsNullOrEmpty(SymbolSuffix) ? packetFIX.Symbol : packetFIX.Symbol.Replace(SymbolSuffix, "");
            if (!Factory.Symbol.TryLookupSymbol(symbolString, out symbolInfo))
            {
                log.Warn("Unable to find " + packetFIX.Symbol + " for execution report.");
                return;
            }
            if (symbolInfo != null)
            {
                algorithm = algorithms.CreateAlgorithm(symbolInfo);
            }

            string orderStatus = packetFIX.OrderStatus;
            switch (orderStatus)
            {
                case "0": // New
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG98, packetFIX);
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
                            if (debug) log.DebugFormat(LogMessage.LOGMSG99, packetFIX);
                            break;
                        }
                    }
                    algorithm.OrderAlgorithm.ConfirmCreate(clientOrderId, Origin.Provider, IsRecovered);
                    TrySendStartBroker(symbolInfo, "sync on confirm cancel");
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "1": // Partial
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG100, packetFIX);
                    }
                    //UpdateOrder(packetFIX, OrderState.Active, null);
                    SendFill(packetFIX);
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "2":  // Filled 
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG101, packetFIX);
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
                        log.DebugFormat(LogMessage.LOGMSG102, packetFIX);
                    }
                    if (algorithm == null)
                    {
                        log.Info("ConfirmChange but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                        break;
                    }
                    algorithm.OrderAlgorithm.ConfirmChange(clientOrderId, originalClientOrderId, Origin.Provider, IsRecovered);
                    TrySendStartBroker(symbolInfo, "sync on confirm change");
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "4": // Canceled
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG103, packetFIX);
                    }
                    if (algorithm == null)
                    {
                        log.Info("Order Canceled but OrderAlgorithm not found for " + symbolInfo + ". Ignoring.");
                        break;
                    }
                    if( originalClientOrderId > 0)
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
                        log.DebugFormat(LogMessage.LOGMSG104, packetFIX);
                    }
                    if (!string.IsNullOrEmpty(packetFIX.Text) && packetFIX.Text.Contains("multifunction order"))
                    {
                        if (debug && (LogRecovery || IsRecovered))
                        {
                            log.DebugFormat(LogMessage.LOGMSG105, packetFIX.ClientOrderId, packetFIX.OriginalClientOrderId);
                        }
                        if( clientOrderId != 0L) OrderStore.RemoveOrder(clientOrderId);
                        //if (originalClientOrderId != 0L) OrderStore.RemoveOrder(originalClientOrderId);
                        break;
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    TryHandlePiggyBackFill(packetFIX);
                    break;
                case "8": // Rejected
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG106, packetFIX);
                    }
                    RejectOrder(packetFIX);
                    break;
                case "9": // Suspended
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG107, packetFIX);
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "A": // PendingNew
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG108, packetFIX);
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
                            if (debug) log.DebugFormat(LogMessage.LOGMSG109);
                        }
                        else
                        {
                            algorithm.OrderAlgorithm.ConfirmCreate(clientOrderId, Origin.Provider, IsRecovered);
                        }
                    }
                    else
                    {
                        algorithm.OrderAlgorithm.ConfirmActive(clientOrderId, Origin.Provider, IsRecovered);
                    }
                    TrySendStartBroker(symbolInfo, "sync on confirm cancel orig order");
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    break;
                case "E": // Pending Replace
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG110, packetFIX);
                    }
                    OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);
                    TryHandlePiggyBackFill(packetFIX);
                    break;
                case "R": // Resumed.
                    if (debug && (LogRecovery || !IsRecovery))
                    {
                        log.DebugFormat(LogMessage.LOGMSG111, packetFIX);
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
                if (debug) log.DebugFormat(LogMessage.LOGMSG112, packetFIX.LastQuantity);
                SendFill(packetFIX);
            }
        }

        private void CancelRejected(MessageFIX4_2 packetFIX)
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG113, packetFIX);
            HandleCancelReject(packetFIX.ClientOrderId, packetFIX.Text, packetFIX);
        }


        public void SendFill(MessageFIX4_2 packetFIX) {
            var clientOrderId = 0L;
            long.TryParse(packetFIX.ClientOrderId, out clientOrderId);
            var originalClientOrderId = 0L;
            long.TryParse(packetFIX.ClientOrderId, out originalClientOrderId);
            if (debug) log.DebugFormat(LogMessage.LOGMSG114, packetFIX.ClientOrderId);
            SymbolInfo symbolInfo;
            var symbolString = string.IsNullOrEmpty(SymbolSuffix) ? packetFIX.Symbol : packetFIX.Symbol.Replace(SymbolSuffix, "");
            if (!Factory.Symbol.TryLookupSymbol(symbolString, out symbolInfo))
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
                    configTime.AddSeconds(timeZone.UtcOffset(executionTime));
                    var fill = Factory.Utility.PhysicalFill(symbolInfo, fillPosition, packetFIX.LastPrice, configTime, executionTime, order.BrokerOrder, false, packetFIX.OrderQuantity, packetFIX.CumulativeQuantity, packetFIX.LeavesQuantity, IsRecovered, true);
                    if (debug) log.DebugFormat(LogMessage.LOGMSG115, fill);
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

        public override void SetupDefaultProperties(string[] sections, ConfigFile configFile)
        {
            foreach (var section in sections)
            {
                configFile.AssureValue(section + "/DisableChangeOrders", "true");
                configFile.AssureValue(section + "/Algorithm", "SORT");
            }
            configFile.AssureValue("Simulate/DisableChangeOrders", "true");
            base.SetupDefaultProperties(sections, configFile);
        }

        public void RejectOrder(MessageFIX4_2 packetFIX)
        {
            HandleOrderReject(packetFIX.ClientOrderId, packetFIX.Symbol, packetFIX.Text, packetFIX);
        }

        public override bool OnCreateBrokerOrder(PhysicalOrder physicalOrder)
        {
            if (!IsRecovered) return false;
            if (debug) log.DebugFormat(LogMessage.LOGMSG116, physicalOrder, ConnectionStatus, isOrderServerOnline);
            if (physicalOrder.Action != OrderAction.Create)
            {
                throw new InvalidOperationException("Expected action Create but was " + physicalOrder.Action);
            }
            OnCreateOrChangeBrokerOrder(physicalOrder, false);
            return true;
        }

        private void OnCreateOrChangeBrokerOrder(PhysicalOrder order, bool resend)
        {
            var fixMsg = (FIXMessage4_2)(resend ? FixFactory.Create(order.Sequence) : FixFactory.Create());
            order.Sequence = fixMsg.Sequence;
            OrderStore.SetOrder(order);
            OrderStore.SetSequences(RemoteSequence, FixFactory.LastSequence);

            if (order.RemainingSize > order.Symbol.MaxOrderSize)
            {
                throw new ApplicationException("Order was greater than MaxOrderSize of " + order.Symbol.MaxPositionSize + " for:\n" + order);
            }

            var orderHandler = algorithms.CreateAlgorithm(order.Symbol);
            var orderSize = order.Side == OrderSide.Sell ? -order.RemainingSize : order.RemainingSize;
            if (Math.Abs(orderHandler.OrderAlgorithm.ActualPosition + orderSize) > order.Symbol.MaxPositionSize)
            {
                throw new ApplicationException("Order was greater than MaxPositionSize of " + order.Symbol.MaxPositionSize + " for:\n" + order);
            }

            if (debug) log.DebugFormat(LogMessage.LOGMSG117, order);
			if( order.Action == OrderAction.Change) {
                var origBrokerOrder = order.OriginalOrder.BrokerOrder;
                fixMsg.SetClientOrderId(order.BrokerOrder.ToString());
				fixMsg.SetOriginalClientOrderId(origBrokerOrder.ToString());
                PhysicalOrder origOrder;
                if (OrderStore.TryGetOrderById(origBrokerOrder, out origOrder))
                {
                    origOrder.ReplacedBy = order;
                    if (debug) log.DebugFormat(LogMessage.LOGMSG118, origBrokerOrder, order.BrokerOrder);
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
                fixMsg.SetAlgorithm( algorithmName);
            }
            fixMsg.SetSymbol(order.Symbol.BaseSymbol+SymbolSuffix);
            fixMsg.SetSide(order.Side == OrderSide.Buy ? 1 : 5);
			switch( order.Type) {
                case OrderType.Limit:
                    fixMsg.SetOrderType(2);
                    fixMsg.SetPrice(order.Price);
                    fixMsg.SetTimeInForce(0); // Lime only supports Day orders.
                    if( order.Symbol.IceBergOrderSize > 0)
                    {
                        fixMsg.SetMaxFloor(order.Symbol.IceBergOrderSize);
                    }
                    break;
                case OrderType.Market:
                    fixMsg.SetOrderType(1);
                    //fixMsg.SetTimeInForce(0);
                    break;
                case OrderType.Stop:
                    throw new LimeException("Lime does not accept Buy Stop Orders: " + order);
                default:
                    throw new LimeException("Unknown OrderType");
            }
            fixMsg.SetOrderQuantity((int)order.CompleteSize);
            if (order.Action == OrderAction.Change)
            {
                if (verbose) log.VerboseFormat(LogMessage.LOGMSG119, fixMsg);
            }
            else
            {
                if (verbose) log.VerboseFormat(LogMessage.LOGMSG120, fixMsg);
            }
            if (resend)
            {
                fixMsg.SetDuplicate(true);
            }
            fixMsg.SetSendTime(order.UtcCreateTime);
            SendMessage(fixMsg);
        }

        protected override void ParseProperties(ConfigFile configFile)
        {
            base.ParseProperties(configFile);
            algorithmName = GetField("Algorithm", configFile, false);
        }

        protected override void ResendOrder(PhysicalOrder order)
        {
            if (order.Action == OrderAction.Cancel)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG121, order);
                //if (SyncTicks.Enabled && !IsRecovered)
                //{
                //    TryAddPhysicalOrder(order);
                //}
                SendCancelOrder(order, true);
            }
            else
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG122, order);
                //if (SyncTicks.Enabled && !IsRecovered)
                //{
                //    TryAddPhysicalOrder(order);
                //}
                OnCreateOrChangeBrokerOrder(order, true);
            }
        }

        public override bool OnCancelBrokerOrder(PhysicalOrder order)
        {
            if (!IsRecovered) return false;
            if (debug) log.DebugFormat(LogMessage.LOGMSG123, order, ConnectionStatus, isOrderServerOnline);
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
            if (!object.ReferenceEquals(order.OriginalOrder, physicalOrder))
            {
                throw new ApplicationException("Different objects!");
            }

            SendCancelOrder(order, false);
            return true;

        }

        private void SendCancelOrder(PhysicalOrder order, bool resend)
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

        public override  bool OnChangeBrokerOrder(PhysicalOrder physicalOrder)
        {
            if (!IsRecovered) return false;
            if (debug) log.DebugFormat(LogMessage.LOGMSG124, physicalOrder, ConnectionStatus, isOrderServerOnline);
            if (physicalOrder.Action != OrderAction.Change)
            {
                throw new InvalidOperationException("Expected action Change but was " + physicalOrder.Action);
            }
            OnCreateOrChangeBrokerOrder(physicalOrder, false);
            return true;
        }

    }
}
