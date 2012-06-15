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
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public enum Status
    {
        None,
        New,
        Connected,
        PendingLogin,
        PendingServerResend,
        PendingRecovery,
        Recovered,
        Disconnected,
        PendingRetry,
        PendingLogOut,
    }

    public abstract class FIXTConnection
    {
        private Socket socket;
        public FIXTConnection( Socket socket)
        {
            this.socket = socket;
        }
    }

    public abstract class FIXProviderSupport : AgentPerformer, PhysicalOrderHandler, LogAware
    {
        private PhysicalOrderStore orderStore;
        private readonly Log log;
        private readonly Log fixLog;
        private volatile bool trace;
        private volatile bool debug;
        private volatile bool verbose;
        private volatile bool fixDebug;
        private Agent receiver;
        public virtual void RefreshLogLevel()
        {
            if (log != null)
            {
                debug = log.IsDebugEnabled;
                trace = log.IsTraceEnabled;
            }
            if (fixLog != null)
                fixDebug = fixLog.IsDebugEnabled;
        }

        protected SymbolReceivers symbolReceivers = new SymbolReceivers();
		private Task socketTask;
		private string failedFile;
		protected Agent ClientAgent;
        private string addrStr;
		private ushort port;
		private string userName;
		private	string password;
		private	string accountNumber;
        private bool disableChangeOrders;
        private string destination;
        private string symbolSuffix;
        private string providerName;
        private TrueTimer heartbeatTimer;
		private long heartbeatDelay;
		private bool logRecovery = true;
        private string configSection;
        private bool useLocalFillTime = true;
		private FIXTFactory fixFactory;
	    private string appDataFolder;
        private TimeStamp lastMessageTime;
        private int remoteSequence = 1;
        private SocketState lastSocketState = SocketState.New;
        private FastQueue<MessageFIXT1_1> resendQueue;
        private volatile Status connectionStatus = Status.None;
        private volatile Status bestConnectionStatus = Status.None;
        protected string name;
        private Agent agent;
        public Agent Agent
        {
            get { return agent; }
            set { agent = value; }
        }
        
		public bool UseLocalFillTime {
			get { return useLocalFillTime; }
		}

        public void DumpHistory()
        {
            for( var i = 0; i<=fixFactory.LastSequence; i++)
            {
                FIXTMessage1_1 message;
                if( fixFactory.TryGetHistory(i, out message)) {
                    log.Info(message.ToString());
                }
            }
        }

        protected SocketReconnect SocketReconnect;

		public FIXProviderSupport(string name)
		{
		    this.name = name;
            configSection = name;
            this.providerName = GetType().Name;
            log = Factory.SysLog.GetLogger(typeof(FIXProviderSupport) + "." + providerName + "." + name);
            log.Register(this);
            fixLog = Factory.SysLog.GetLogger(typeof(FIXProviderSupport).Namespace + ".FIXLog." + providerName + "." + name);
            fixLog.Register(this);
            verbose = log.IsVerboseEnabled;
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
            fixDebug = fixLog.IsDebugEnabled;
            algorithms = new SymbolAlgorithms(this);
        }

        public void Start(EventItem eventItem)
        {
            if (debug) log.DebugFormat("Start() Agent: " + eventItem.Agent);
            this.ClientAgent = (Agent) eventItem.Agent;
            log.Info(providerName + " Startup");
            if (CheckFailedLoginFile())
            {
                Dispose();
            }
            else
            {
                SocketReconnect.Regenerate();
            }
        }

        public void Initialize(Task task)
        {
            socketTask = task;
            socketTask.Scheduler = Scheduler.EarliestTime;
            heartbeatTimer = Factory.Parallel.CreateTimer("Heartbeat", socketTask, HeartBeatTimerEvent);
            resendQueue = Factory.Parallel.FastQueue<MessageFIXT1_1>(providerName + "." + name + ".Resend");
            orderStore = Factory.Utility.PhyscalOrderStore(providerName + "." + name);
            resendQueue.ConnectInbound(socketTask);
            socketTask.Start();
            string logRecoveryString = Factory.Settings["LogRecovery"];
            logRecovery = !string.IsNullOrEmpty(logRecoveryString) && logRecoveryString.ToLower().Equals("true");
            appDataFolder = Factory.Settings["AppDataFolder"];
            if (appDataFolder == null)
            {
                throw new ApplicationException("Sorry, AppDataFolder must be set.");
            }
            if (debug) log.DebugFormat("> SetupFolders.");
            string configFile = appDataFolder + @"/Providers/" + providerName + "/Default.config";
            failedFile = appDataFolder + @"/Providers/" + providerName + "/LoginFailed.txt";
            if (File.Exists(failedFile))
            {
                log.Error("Please correct the username or password error described in " + failedFile + ". Then delete the file before retrying, please.");
                return;
            }
            LoadProperties(configFile);
            algorithms.DisableChangeOrders = disableChangeOrders;
            SocketReconnect = new SocketReconnect(providerName, name, task, addrStr, port, CreateMessageFactory(), OnConnect, OnDisconnect);
        }

        protected abstract MessageFactory CreateMessageFactory();


		public void WriteFailedLoginFile(string packetString) {
			string message = "Login failed for user name: " + userName + " and password: " + new string('*',password.Length);
			string fileMessage = "Resolve the problem and then delete this file before you retry.";
			string logMessage = "Resolve the problem and then delete the file " + failedFile + " before you retry.";
			if( File.Exists(failedFile)) {
				File.Delete(failedFile);
			}
			using( var fileOut = new StreamWriter(failedFile)) {
				fileOut.WriteLine(message);
				fileOut.WriteLine(fileMessage);
				fileOut.WriteLine("Actual FIX message for login that failed:");
				fileOut.WriteLine(packetString);
			}
			log.Error(message + " " + logMessage + "\n" + packetString);
		}
		
        public bool IsInterrupted
        {
			get {
				return isDisposed || SocketReconnect.State != SocketState.Connected;
			}
		}

        public virtual void CancelRecovered()
        {
            ConnectionStatus = Status.PendingRecovery;
            foreach (var algorithm in algorithms.GetAlgorithms())
            {
                algorithm.OrderAlgorithm.IsPositionSynced = false;
            }
        }

		public void StartRecovery() {
            CancelRecovered();
			OnStartRecovery();
		}
		
		public void EndRecovery() {
			ConnectionStatus = Status.Recovered;
		    bestConnectionStatus = Status.Recovered;
            OrderStore.ResetLastChange();
            OnFinishRecovery();
        }
		
		public bool IsRecovered {
			get {
                return ConnectionStatus == Status.Recovered;
			}
		}
		
		public bool IsRecovery {
			get {
				return ConnectionStatus == Status.PendingRecovery;
			}
		}

        private bool SnapshotBusy
        {
            get { return orderStore.IsBusy; }
        }

        private int frozenHeartbeatCounter;
        private Yield HeartBeatTimerEvent()
        {
            var typeStr = ConnectionStatus == Status.PendingLogin ? "Login Timeout" : "Heartbeat timeout";
            log.Info(typeStr + ". Last Message UTC Time: " + lastMessageTime + ", current UTC Time: " + TimeStamp.UtcNow);
            log.Error("FIXProvider " + typeStr);
            if (SyncTicks.Frozen)
            {
                frozenHeartbeatCounter++;
                if (frozenHeartbeatCounter > 3)
                {
                    if (debug) log.DebugFormat("More than 3 heart beats sent after frozen.  Ending heartbeats.");
                    HeartbeatDelay = 50;
                }
                else
                {
                    orderStore.ForceSnapshot();
                    SocketReconnect.Regenerate();
                }
            }
            else
            {
                orderStore.ForceSnapshot();
                SocketReconnect.Regenerate();
            }
            IncreaseHeartbeatTimeout();
            return Yield.DidWork.Repeat;
        }

        public void Shutdown()
        {
            LogOut();
        }

        public Yield Invoke()
        {
            if (isDisposed) return Yield.NoWork.Repeat;

            EventItem eventItem;
            if( socketTask.Filter.Receive(out eventItem))
            {
                switch (eventItem.EventType)
                {
                    case EventType.Connect:
                        receiver = eventItem.Agent;
                        Start(eventItem);
                        socketTask.Filter.Pop();
                        break;
                    case EventType.Disconnect:
                        Stop(eventItem);
                        socketTask.Filter.Pop();
                        break;
                    case EventType.StartBroker:
                        StartBroker(eventItem);
                        socketTask.Filter.Pop();
                        break;
                    case EventType.StopBroker:
                        StopBroker(eventItem);
                        socketTask.Filter.Pop();
                        break;
                    case EventType.PositionChange:
                        var positionChange = (PositionChangeDetail) eventItem.EventDetail;
                        PositionChange(positionChange);
                        socketTask.Filter.Pop();
                        break;
                    case EventType.SyntheticConfirmation:
                        SyntheticConfirmation( (CreateOrChangeOrder) eventItem.EventDetail);
                        socketTask.Filter.Pop();
                        break;
                    case EventType.SyntheticFill:
                        SyntheticFill((PhysicalFill)eventItem.EventDetail);
                        socketTask.Filter.Pop();
                        break;
                    case EventType.SyntheticReject:
                        SyntheticReject((CreateOrChangeOrder)eventItem.EventDetail);
                        socketTask.Filter.Pop();
                        break;
                    case EventType.Shutdown:
                        LogOut();
                        socketTask.Filter.Pop();
                        break;
                    case EventType.Terminate:
                        Dispose();
                        socketTask.Filter.Pop();
                        break;
                    default:
                        throw new ApplicationException("Unexpected event type: " + eventItem.EventType);
                }
            }

            if (SnapshotBusy) return Yield.DidWork.Repeat;

            if (SocketReconnect == null) return Yield.DidWork.Repeat;

            if (SocketReconnect.State != lastSocketState)
            {
                if (debug) log.DebugFormat("SocketState changed to " + SocketReconnect.State);
                lastSocketState = SocketReconnect.State;
            }
            switch (SocketReconnect.State)
            {
                case SocketState.New:
                    return Yield.NoWork.Repeat;
                case SocketState.PendingConnect:
                    return Yield.NoWork.Repeat;
                case SocketState.Connected:
                    var transaction = OrderStore.BeginTransaction();
                    try
                    {
                        var result = TryProcessMessage();
                        if (!result.IsIdle)
                        {
                            orderStore.TrySnapshot();
                        }
                        return result;
                    }
                    finally
                    {
                        transaction.Dispose();
                    }
                case SocketState.Disconnected:
                case SocketState.Closed:
                    return TrySetupRetry();
                case SocketState.ShuttingDown:
                case SocketState.Closing:
                    return Yield.NoWork.Repeat;
                default:
                    string textMessage = "Unknown socket state: " + SocketReconnect.State;
                    log.Error(textMessage);
                    throw new ApplicationException(textMessage);
            }
		}

        private void SyntheticReject(CreateOrChangeOrder order)
        {
            if (debug) log.DebugFormat("SyntheticReject: " + order);
            var orderAlgorithm = algorithms.CreateAlgorithm(order.Symbol);
            var algorithm = orderAlgorithm.OrderAlgorithm;
            var retryImmediately = algorithm.RejectRepeatCounter == 0;
            algorithm.RejectOrder(order.BrokerOrder, IsRecovered, true);
            if (!retryImmediately)
            {
                TrySendEndBroker(order.Symbol);
            }
        }

        private void SyntheticFill(PhysicalFill fill)
        {
            if( debug) log.DebugFormat("SyntheticFill: " + fill);
            var orderAlgorithm = algorithms.CreateAlgorithm(fill.Symbol);
            orderAlgorithm.OrderAlgorithm.SyntheticFill(fill);
        }

        private void SyntheticConfirmation(CreateOrChangeOrder order)
        {
            var orderAlgorithm = algorithms.CreateAlgorithm(order.Symbol);
            switch( order.Action)
            {
                case OrderAction.Create:
                    orderAlgorithm.OrderAlgorithm.ConfirmCreate(order.BrokerOrder, IsRecovered);
                    break;
                case OrderAction.Change:
                    orderAlgorithm.OrderAlgorithm.ConfirmChange(order.BrokerOrder, IsRecovered);
                    break;
                case OrderAction.Cancel:
                    orderAlgorithm.OrderAlgorithm.ConfirmCancel(order.OriginalOrder.BrokerOrder, IsRecovered);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private Yield TrySetupRetry()
        {
            switch (ConnectionStatus)
            {
                case Status.PendingLogin:
                case Status.PendingServerResend:
                case Status.PendingRecovery:
                case Status.Recovered:
                    return TryProcessMessage();
                case Status.Connected:
                case Status.Disconnected:
                case Status.New:
                    ConnectionStatus = Status.PendingRetry;
                    SocketReconnect.IncreaseRetry();
                    return Yield.NoWork.Repeat;
                case Status.PendingRetry:
                    return Yield.NoWork.Repeat;
                case Status.PendingLogOut:
                    Dispose();
                    return Yield.NoWork.Repeat;
                default:
                    log.Warn("Unexpected connection status when socket disconnected: " + ConnectionStatus + ". Shutting down the provider.");
                    Dispose();
                    return Yield.NoWork.Repeat;
            }
        }

        private Yield TryProcessMessage()
        {
            switch (ConnectionStatus)
            {
                case Status.New:
                case Status.Connected:
                    return Yield.NoWork.Repeat;
                case Status.PendingLogin:
                case Status.PendingLogOut:
                case Status.PendingServerResend:
                case Status.PendingRecovery:
                case Status.Recovered:
                    SocketReconnect.ResetRetry();

                    MessageFIXT1_1 messageFIX = null;

                    if (ConnectionStatus != Status.PendingLogin)
                    {
                        while (resendQueue.Count > 0)
                        {
                            MessageFIXT1_1 tempMessage;
                            resendQueue.Peek(out tempMessage);
                            if (tempMessage.Sequence > remoteSequence && IsResendComplete && connectionStatus != Status.PendingServerResend)
                            {
                                if (debug) log.DebugFormat("Found sequence " + tempMessage.Sequence + " on the resend queue. Requesting resend from " + remoteSequence + " to " + (tempMessage.Sequence - 1));
                                TryRequestResend(remoteSequence, tempMessage.Sequence - 1);
                                break;
                            }
                            if (tempMessage.Sequence == remoteSequence)
                            {
                                resendQueue.Dequeue(out messageFIX);
                                break;
                            }
                            if (tempMessage.Sequence < remoteSequence)
                            {
                                resendQueue.Dequeue(out messageFIX);
                            }
                            if (tempMessage.Sequence > remoteSequence)
                            {
                                break;
                            }
                        }
                    }

                    if (messageFIX == null)
                    {
                        Message message = null;
                        if (SocketReconnect.TryGetMessage(out message))
                        {
                            var disconnect = message as DisconnectMessage;
                            if (disconnect == null)
                            {
                                messageFIX = (MessageFIXT1_1)message;
                            }
                            else
                            {
                                SocketReconnect.OnDisconnect(disconnect.Socket);
                            }
                        }
                    }

                    if (messageFIX != null)
                    {
                        if( fixDebug) LogMessage(messageFIX.ToString(), true);
                        if (debug) log.DebugFormat("Received FIX Message: " + messageFIX);
                        if (messageFIX.MessageType == "A")
                        {
                            if (messageFIX.Sequence == 1 || messageFIX.Sequence < remoteSequence)
                            {
                                remoteSequence = messageFIX.Sequence;
                                if (debug) log.DebugFormat("FIX Server login message sequence was lower than expected. Resetting to " + remoteSequence);
                                orderStore.LastSequenceReset = new TimeStamp(messageFIX.SendUtcTime);
                            }
                            if (HandleLogon(messageFIX))
                            {
                                ConnectionStatus = Status.PendingServerResend;
                                TryEndRecovery();
                            }
                        }
                    }

                    if (messageFIX != null)
                    {
                        lastMessageTime = TimeStamp.UtcNow;
                        //switch (messageFIX.MessageType)
                        //{
                        //    case "2":
                        //        break;
                        //}
                        var releaseFlag = true;
                        var message4_4 = messageFIX as MessageFIX4_4;
                        // Workaround for bug in MBT FIX Server that sends message from prior to 
                        // last sequence number reset.
                        if (messageFIX.IsPossibleDuplicate &&
                            message4_4 != null && message4_4.OriginalSendTime != null &&
                            new TimeStamp(message4_4.OriginalSendTime) < OrderStore.LastSequenceReset)
                        {
                            log.Info("Ignoring message with sequence " + messageFIX.Sequence + " as work around to MBT FIX server defect because original send time (122) " + new TimeStamp(message4_4.OriginalSendTime) + " is prior to last sequence reset " + OrderStore.LastSequenceReset);
                        }
                        else
                        {
                            var handledAlready = false;
                            switch (messageFIX.MessageType)
                            {
                                case "1":
                                    IsResendComplete = true;
                                    break;      
                                case "2":
                                    HandleResend(messageFIX);
                                    handledAlready = true;
                                    break;
                            }
                            if (connectionStatus == Status.PendingServerResend)
                            {
                                if (CheckForServerSync(messageFIX))
                                {
                                    if (ConnectionStatus == Status.PendingServerResend)
                                    {
                                        ConnectionStatus = Status.PendingRecovery;
                                        TryEndRecovery();
                                    }
                                }
                            }
                            if (!CheckForMissingMessages(messageFIX, out releaseFlag) && !handledAlready)
                            {
                                switch (messageFIX.MessageType)
                                {
                                    case "A": //logon
                                        // Already handled and sequence incremented.
                                        break;
                                    case "2": // resend
                                        HandleResend(messageFIX);
                                        break;
                                    case "4": // gap fill
                                        HandleGapFill(messageFIX);
                                        break;
                                    case "3": // reject
                                        HandleReject(messageFIX);
                                        break;
                                    case "5": // log off confirm
                                        if (debug) log.DebugFormat("Logout message received.");
                                        if (ConnectionStatus == Status.PendingLogOut)
                                        {
                                            Dispose();
                                        }
                                        else
                                        {
                                            HandleUnexpectedLogout(messageFIX);
                                        }
                                        break;
                                    default:
                                        ReceiveMessage(messageFIX);
                                        break;
                                }
                                orderStore.UpdateRemoteSequence(remoteSequence);
                            }
                        }
                        if (releaseFlag)
                        {
                            SocketReconnect.Release(messageFIX);
                        }
                        orderStore.IncrementUpdateCount();
                        IncreaseHeartbeatTimeout();
                        return Yield.DidWork.Repeat;
                    }
                    else
                    {
                        return Yield.NoWork.Repeat;
                    }
                case Status.Disconnected:
                    return Yield.NoWork.Repeat;
                default:
                    throw new InvalidOperationException("Unknown connection status: " + ConnectionStatus);
            }
        }

        protected abstract bool CheckForServerSync(MessageFIXT1_1 messageFix);

        protected virtual void HandleUnexpectedLogout(MessageFIXT1_1 message) {
            SocketReconnect.Socket.Dispose();
        }

        protected virtual bool HandleLogon(MessageFIXT1_1 message)
        {
            throw new NotImplementedException();
        }

        private void HandleGapFill(MessageFIXT1_1 packetFIX)
        {
            if (!packetFIX.IsGapFill)
            {
                throw new InvalidOperationException("Only gap fill sequence reset supportted: \n" + packetFIX);
            }
            if (packetFIX.NewSeqNum < RemoteSequence)
            {
                throw new InvalidOperationException("Reset new sequence number must be greater than or equal to the next sequence: " + RemoteSequence + ".\n" + packetFIX);
            }
            RemoteSequence = packetFIX.NewSeqNum;
            if (packetFIX.Sequence > RemoteSequence)
            {
                HandleResend(packetFIX.Sequence, packetFIX);
            }
            if (debug) log.DebugFormat("Received gap fill. Setting next sequence = " + RemoteSequence);
        }

        private void HandleReject(MessageFIXT1_1 packetFIX)
        {
            if (packetFIX.Text.Contains("Sending Time Accuracy problem"))
            {
                if (debug) log.DebugFormat("Found Sending Time Accuracy message request for message: " + packetFIX.ReferenceSequence );
                FIXTMessage1_1 textMessage;
                if (!fixFactory.TryGetHistory(packetFIX.ReferenceSequence, out textMessage))
                {
                    log.Warn("Unable to find message " + packetFIX.ReferenceSequence + ". This is probably due to being prior to recent restart. Ignoring.");
                }
                else
                {
                    if( debug) log.DebugFormat("Sending Time Accuracy Problem -- Resending Message.");
                    textMessage.Sequence = fixFactory.GetNextSequence();
                    textMessage.SetDuplicate(true);
                    SendMessageInternal( textMessage);
                }
                return;
            }
            else
            {
                if (debug) log.DebugFormat("Starting history dump");
                if ( debug) DumpHistory();
                if (debug) log.DebugFormat("Fiinished history dump");
                throw new ApplicationException("Received administrative reject message with unrecognized error message: '" + packetFIX.Text + "'");
            }
        }

        private bool CheckFailedLoginFile()
	    {
            return File.Exists(failedFile);
        }

	    private int expectedResendSequence;
	    private bool isResendComplete = true;

        private bool CheckForMissingMessages(MessageFIXT1_1 messageFIX, out bool releaseFlag)
        {
            releaseFlag = true;
            if( messageFIX.MessageType == "A")
            {
                if (messageFIX.Sequence == RemoteSequence)
                {
                    RemoteSequence = messageFIX.Sequence + 1;
                    if (debug) log.DebugFormat("Login sequence matched. Incrementing remote sequence to " + RemoteSequence);
                }
                else
                {
                    if (debug) log.DebugFormat("Login remote sequence " + messageFIX.Sequence + " mismatch expected sequence " + RemoteSequence + ". Resend needed.");
                }
                return false;
            }
            else if (ConnectionStatus == Status.PendingLogin && messageFIX.MessageType == "5")
            {
                HandleRejectedLogin(messageFIX);
                return true;
            }
            else if (ConnectionStatus == Status.PendingLogin && messageFIX.MessageType != "5")
            {
                resendQueue.Enqueue(messageFIX, TimeStamp.UtcNow.Internal);
                releaseFlag = false;
                return true;
            }
            else if (messageFIX.Sequence > RemoteSequence)
            {
                HandleResend(messageFIX.Sequence, messageFIX);
                releaseFlag = false;
                return true;
            }
            else if( messageFIX.Sequence < RemoteSequence)
            {
                if (debug) log.DebugFormat("Already received sequence " + messageFIX.Sequence + ". Expecting " + RemoteSequence + " as next sequence. Ignoring. \n" + messageFIX);
                return true;
            }
            else
            {
                if (!IsResendComplete && messageFIX.Sequence >= expectedResendSequence)
                {
                    IsResendComplete = true;
                    TryEndRecovery();
                }
                RemoteSequence = messageFIX.Sequence + 1;
                if( debug) log.DebugFormat("Incrementing remote sequence to " + RemoteSequence);
                return false;
            }
        }

        private void HandleResend(int sequence, MessageFIXT1_1 messageFIX) {
            if (debug) log.DebugFormat("Sequence is " + sequence + " but expected sequence is " + RemoteSequence + ". Buffering message.");
            resendQueue.Enqueue(messageFIX, TimeStamp.UtcNow.Internal);

            if( resendQueue.Count > 0)
            {
                MessageFIXT1_1 tempMessage;
                resendQueue.Peek(out tempMessage);
                if (tempMessage.Sequence > remoteSequence)
                {
                    if (debug) log.DebugFormat("Found sequence " + tempMessage.Sequence + " on the resend queue. Requesting resend from " + remoteSequence + " to " + (tempMessage.Sequence - 1));
                    TryRequestResend(remoteSequence, tempMessage.Sequence - 1);
                    return;
                }
            }
            TryRequestResend(remoteSequence,sequence - 1);
        }

        private void TryRequestResend(int from, int to)
        {
            if (IsResendComplete)
            {
                if (connectionStatus == Status.PendingServerResend)
                {
                    log.Notice("Skipping handleResend for status " + connectionStatus);
                }
                else
                {
                    log.Notice("HandleResend status " + connectionStatus);
                    IsResendComplete = false;
                    expectedResendSequence = to;
                    if (debug) log.DebugFormat("Expected resend sequence set to " + expectedResendSequence);
                    var mbtMsg = fixFactory.Create();
                    mbtMsg.AddHeader("2");
                    mbtMsg.SetBeginSeqNum(from);
                    mbtMsg.SetEndSeqNum(to);
                    if (verbose) log.VerboseFormat(" Sending resend request: " + mbtMsg);
                    SendMessage(mbtMsg);
                }
            }
        }

        private FIXTMessage1_1 GapFillMessage(int currentSequence, int newSequence)
        {
            var endText = newSequence == 0 ? "infinity" : newSequence.ToString();
            if (debug) log.DebugFormat("Sending gap fill message " + currentSequence + " to " + endText);
            var message = fixFactory.Create(currentSequence);
            message.SetGapFill();
            message.SetNewSeqNum(newSequence);
            message.AddHeader("4");
            return message;
        }


	    protected abstract void ResendOrder(CreateOrChangeOrder order);


        private bool HandleResend(Message message)
        {
			var messageFIX = (MessageFIXT1_1) message;
			int end = messageFIX.EndSeqNum == 0 ? fixFactory.LastSequence : messageFIX.EndSeqNum;
            if (messageFIX.BegSeqNum == 1)
            {
                // Resend request was from 1. This means that the server restarted the sequence numbers.
                // So we don't want to resend the active orders, simply fill the gap.
                var textMessage = GapFillMessage(messageFIX.BegSeqNum, end + 1);
                SendMessageInternal(textMessage);
                return true;
            }
            if (debug) log.DebugFormat("Found resend request for " + messageFIX.BegSeqNum + " to " + end + ": " + messageFIX);
            if (messageFIX.BegSeqNum <= end)
            {
                var previous = messageFIX.BegSeqNum;
                for( var i = previous; i<=end; i++)
                {
                    CreateOrChangeOrder order;
                    FIXTMessage1_1 missingMessage;
                    var sentFlag = false;
                    if( orderStore.TryGetOrderBySequence( i, out order))
                    {
                        if( previous < i)
                        {
                            missingMessage = GapFillMessage(previous, i);
                            SendMessageInternal(missingMessage);
                        }
                        ResendOrder(order);
                        sentFlag = true;
                    }
                    else if( fixFactory.TryGetHistory( i, out missingMessage))
                    {
                        missingMessage.SetDuplicate(true);
                        switch (missingMessage.Type)
                        {
                            case "g":
                            case "5": // Logoff
                            case "2":
                                if (previous < i)
                                {
                                    var gapMessage = GapFillMessage(previous, i);
                                    SendMessageInternal(gapMessage);
                                }
                                SendMessageInternal(missingMessage);
                                sentFlag = true;
                                break;
                            case "A":
                            case "0":
                            case "AN":
                            case "G":
                            case "F":
                                break;
                            default:
                                log.Warn("Message type " + missingMessage.Type + " skipped during resend: " + missingMessage);
                                break;
                        }
                    }
                    if( sentFlag)
                    {
                        previous = i + 1;
                    }
                }
                if( previous < end)
                {
                    var textMessage = GapFillMessage(previous, end + 1);
                    SendMessageInternal(textMessage);
                }
            }
            return true;
        }

		protected void IncreaseHeartbeatTimeout()
		{
		    var heartbeatTime = TimeStamp.UtcNow;
            if( SyncTicks.Enabled)
            {
                heartbeatTime.AddMilliseconds(heartbeatDelay * 100);
            }
            else
            {
                heartbeatTime.AddSeconds(heartbeatDelay);
            }
			heartbeatTimer.Start(heartbeatTime);
		}

        protected abstract void ReceiveMessage(Message message);
		
		private void OnException( Exception ex) {
			// Attempt to propagate the exception.
			log.Error("Exception occurred: ", ex);
			SendError( ex.Message);
            Dispose();
		}
		
        public void Stop(EventItem eventItem) {
        	
        }

        public void StartBroker(EventItem eventItem)
        {
            log.Info("StartBroker(" + eventItem.Symbol + ")");
        	// This adds a new order handler.
            symbolReceivers.TryAddSymbol(eventItem.Symbol,eventItem.Agent);
            using( orderStore.BeginTransaction())
            {
                OnStartBroker(eventItem.Symbol);
            }
        }

        public void StopBroker(EventItem eventItem)
        {
        	log.Info("StopBroker( " + eventItem.Symbol + ")");
            if (symbolReceivers.TryRemoveSymbol(eventItem.Symbol))
            {
                OnStopBroker(eventItem.Symbol);
        	}
        }
        
        public abstract void OnStopBroker(SymbolInfo symbol);
	        
        private void LoadProperties(string configFilePath) {
	        log.Notice("Using section " + configSection + " in file: " + configFilePath);
	        var configFile = new ConfigFile(configFilePath);
            var sections = new[] { "EquityDemo", "ForexDemo", "EquityLive", "ForexLive", "ClientTest", "MarketTest", "Simulate" };
            SetupDefaultProperties( sections, configFile);
            ParseProperties(configFile);
		}

        public virtual void SetupDefaultProperties(string[] sections, ConfigFile configFile)
        {
            foreach( var section in sections)
            {
                configFile.AssureValue(section + "/UseLocalFillTime", "true");
                configFile.AssureValue(section + "/ServerAddress", "127.0.0.1");
                configFile.AssureValue(section + "/ServerPort", "5679");
                configFile.AssureValue(section + "/UserName", "CHANGEME");
                configFile.AssureValue(section + "/Password", "CHANGEME");
                configFile.AssureValue(section + "/AccountNumber", "CHANGEME");
                configFile.AssureValue(section + "/SessionIncludes", "*");
                configFile.AssureValue(section + "/SessionExcludes", "");
                configFile.AssureValue(section + "/Destination", "7");
                configFile.AssureValue(section + "/DisableChangeOrders", "false");
                configFile.AssureValue(section + "/SymbolSuffix", "");
            }
            configFile.AssureValue("EquityLive/ServerPort","5680");
            configFile.AssureValue("ForexLive/ServerPort","5680");
            configFile.AssureValue("Simulate/UseLocalFillTime", "false");
            configFile.AssureValue("Simulate/ServerAddress", "127.0.0.1");
            configFile.AssureValue("Simulate/ServerPort", "6489");
            configFile.AssureValue("Simulate/UserName", "Simulate1");
            configFile.AssureValue("Simulate/Password", "only4sim");
            configFile.AssureValue("Simulate/AccountNumber", "11111111");
            configFile.AssureValue("Simulate/DisableChangeOrders", "false");
        }

        private void ParseProperties(ConfigFile configFile)
        {
			var value = GetField("UseLocalFillTime",configFile, false);
			if( !string.IsNullOrEmpty(value)) {
				useLocalFillTime = value.ToLower() != "false";
        	}
			
        	AddrStr = GetField("ServerAddress",configFile, true);
        	var portStr = GetField("ServerPort",configFile, true);
			if( !ushort.TryParse(portStr, out port)) {
				Exception( "ServerPort", configFile);
			}
			userName = GetField("UserName",configFile, true);
			password = GetField("Password",configFile, true);
			accountNumber = GetField("AccountNumber",configFile, true);
            destination = GetField("Destination", configFile, false);
            var disableChangeString = GetField("DisableChangeOrders", configFile, false);
            symbolSuffix = GetField("SymbolSuffix", configFile, false);
            disableChangeOrders = string.IsNullOrEmpty(disableChangeString) ? false : disableChangeString.ToLower() == "true";
            var includeString = GetField("SessionIncludes", configFile, false);
            var excludeString = GetField("SessionExcludes", configFile, false);
            sessionMatcher = new IncludeExcludeMatcher(includeString, excludeString);
        }

        private IncludeExcludeMatcher sessionMatcher;
        
        private string GetField( string field, ConfigFile configFile, bool required) {
			var result = configFile.GetValue(configSection + "/" + field);
			if( required && string.IsNullOrEmpty(result)) {
				Exception( field, configFile);
			}
			return result;
        }

        public bool CompareSession( string session)
        {
            return sessionMatcher.Compare(session);
        }
        
        //TODO: Needs to change to reflect other providers
        private void Exception( string field, ConfigFile configFile) {
        	var sb = new StringBuilder();
        	sb.AppendLine("Sorry, an error occurred finding the '" + field +"' setting.");
        	sb.AppendLine("Please either set '" + field +"' in section '"+configSection+"' of '"+configFile+"'.");
            sb.AppendLine("Otherwise you may choose a different section within the config file.");
            sb.AppendLine("You can choose the section either in your project.tzproj file or");
            sb.AppendLine("if you run a standalone ProviderService, in the ProviderServer\\Default.config file.");
            sb.AppendLine("In either case, you may set the ProviderAssembly value as <AssemblyName>/<Section>");
            sb.AppendLine("For example, MBTFIXProvider/EquityDemo will choose the MBTFIXProvider.exe assembly");
            sb.AppendLine("with the EquityDemo section within the MBTFIXProvider\\Default.config file for that assembly.");
            throw new ApplicationException(sb.ToString());
        }
        
		private string UpperFirst(string input)
		{
			string temp = input.Substring(0, 1);
			return temp.ToUpper() + input.Remove(0, 1);
		}        
		
		public void SendError(string error) {
			if( ClientAgent!= null) {
				ErrorDetail detail = new ErrorDetail();
				detail.ErrorMessage = error;
				log.Error(detail.ErrorMessage);
			}
		}

        private bool isFinalized;

	 	protected volatile bool isDisposed = false;

        protected SymbolAlgorithms algorithms;

        protected TimeStamp previousHeartbeatTime = default(TimeStamp);
        protected TimeStamp recentHeartbeatTime = default(TimeStamp);
        protected volatile bool isOrderServerOnline = false;

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
	            	if( debug) log.DebugFormat("Dispose()");
                    if (connectionStatus != Status.PendingLogOut)
                    {
                        SyncTicks.Success = false;
                        log.Error("The FIX provider " + providerName + " ended in state: " + connectionStatus);
                    }
                    else
                    {
                        log.Info("The FIX provider " + providerName + " completed in logged out state.");
                    }
                    if (socketTask != null)
                    {
		            	socketTask.Stop();
                        socketTask.Join();
	            	}
                    if (SocketReconnect != null)
                    {
                        SocketReconnect.Dispose();
                    }
                    if( heartbeatTimer != null)
                    {
                        heartbeatTimer.Dispose();
                    }
                    if (orderStore != null)
                    {
                        orderStore.Dispose();
                    }
	                isFinalized = true;
	            }
    		}
	    }    

	    public void SendMessage(FIXTMessage1_1 fixMsg) {
            if( !fixMsg.IsDuplicate)
            {
                fixFactory.AddHistory(fixMsg);
            }
            SendMessageInternal(fixMsg);
            OrderStore.UpdateLocalSequence(FixFactory.LastSequence);
        }
		
	    private void SendMessageInternal(FIXTMessage1_1 fixMsg) {
			var fixString = fixMsg.ToString();
            if (fixDebug) LogMessage(fixString, false);
            else if (debug)
            {
				string view = fixString.Replace(FIXTBuffer.EndFieldStr,"  ");
				log.DebugFormat("Send FIX message: \n" + view);
			}

	        var packet = SocketReconnect.CreateMessage();
	        packet.SendUtcTime = fixMsg.SendTime.Internal;
			packet.DataOut.Write(fixString.ToCharArray());
            if( fixMsg.Type == "D")
            {
                var current = TimeStamp.UtcNow;
                var elapsed = current - fixMsg.SendTime;
                log.Info("Latency round trip time " + elapsed + " from " + fixMsg.SendTime + " to " + current);
            }
			var end = Factory.Parallel.TickCount + (long)heartbeatDelay * 1000L;
			while( !SocketReconnect.TrySendMessage(packet)) {
				if( IsInterrupted) return;
				if( Factory.Parallel.TickCount > end) {
					throw new ApplicationException("Timeout while sending message.");
				}
				Factory.Parallel.Yield();
			}
	        lastMessageTime = Factory.Parallel.UtcNow;
            orderStore.IncrementUpdateCount();
        }

        protected virtual void TryEndRecovery()
        {
            throw new NotImplementedException();
        }

        public void LogOut()
        {
            if (bestConnectionStatus != Status.Recovered)
            {
                Dispose();
                return;
            }
            if(!isDisposed)
            {
                if( debug) log.DebugFormat("LogOut() status " + ConnectionStatus);
                switch( ConnectionStatus)
                {
                    case Status.Connected:
                    case Status.Disconnected:
                    case Status.New:
                    case Status.None:
                    case Status.PendingLogin:
                    case Status.PendingRetry:
                        Dispose();
                        break;
                    case Status.PendingLogOut:
                        break;
                    case Status.Recovered:
                    case Status.PendingServerResend:
                    case Status.PendingRecovery:
                        ConnectionStatus = Status.PendingLogOut;
                        using (orderStore.BeginTransaction())
                        {
                            if (debug) log.DebugFormat("Calling OnLogOut()");
                            OnLogout();
                        }
                        break;
                    default:
                        throw new ApplicationException("Unexpected connection status for log out: " + ConnectionStatus);
                }
            }
        }

		public string AddrStr {
			get { return addrStr; }
			set { addrStr = value; }
		}
		
		public ushort Port {
			get { return port; }
			set { port = value; }
		}
		
		public string UserName {
			get { return userName; }
			set { userName = value; }
		}
		
		public string Password {
			get { return password; }
			set { password = value; }
		}
		
		public string ProviderName {
			get { return providerName; }
			set { providerName = value; }
		}
		
		public long HeartbeatDelay {
	    	get { return heartbeatDelay; }
			set {
                heartbeatDelay = value;
				IncreaseHeartbeatTimeout();
			}
		}
		
		public bool LogRecovery {
			get { return logRecovery; }
		}

        public string Destination
        {
            get { return destination; }
        }

		public string AccountNumber {
			get { return accountNumber; }
		}
		
		public FIXTFactory FixFactory {
			get { return fixFactory; }
			set { fixFactory = value; }
		}

	    public int RemoteSequence
	    {
	        get { return remoteSequence; }
	        set
	        {
	            remoteSequence = value;
	        }
	    }

	    public PhysicalOrderStore OrderStore
	    {
	        get { return orderStore; }
	    }

	    public bool IsResendComplete
	    {
	        get { return isResendComplete; }
	        set
	        {
	            if( isResendComplete != value)
	            {
                    if (debug) log.DebugFormat("Resend Complete changed to " + value);
	                isResendComplete = value;
	            }
	        }
	    }

        public bool IsFinalized
        {
            get { return isFinalized; }
        }

        public bool IsChanged
        {
            get { return false; }
            set { }
        }

        public Agent Receiver
        {
            get { return receiver; }
        }

        private unsafe void LogMessage(string messageFIX, bool received)
        {
            fixLog.DebugFormat("{0}: {1}", received ? "RCV" : "SND", messageFIX);
        }

        protected void TrySend(EventType type, SymbolInfo symbol, Agent agent)
        {
            if( debug) log.DebugFormat("Sending " + type + " for " + symbol + " ...");
            var item = new EventItem(symbol, type);
            agent.SendEvent(item);
        }

        protected void TryRequestPosition(SymbolInfo symbol)
        {
            var symbolReceiver = symbolReceivers.GetSymbolRequest(symbol);
            if (!IsRecovered)
            {
                if (debug) log.DebugFormat("Attempted RequestPosition but IsRecovered is " + IsRecovered);
                return;
            }
            var symbolAlgorithm = algorithms.CreateAlgorithm(symbol);
            if (symbolAlgorithm.OrderAlgorithm.IsBrokerOnline)
            {
                if (debug) log.DebugFormat("Attempted RequestPosition but isBrokerStarted is " + symbolAlgorithm.OrderAlgorithm.IsBrokerOnline);
                return;
            }
            TrySend(EventType.RequestPosition, symbol, symbolReceiver.Agent);
        }

        protected void TrySendStartBroker(SymbolInfo symbol, string message)
        {
            SymbolReceiver symbolReceiver;
            if( !symbolReceivers.TryGetSymbolRequest(symbol, out symbolReceiver))
            {
                return;
            }
            if( !IsRecovered)
            {
                if (debug) log.DebugFormat("Attempted StartBroker but IsRecovered is " + IsRecovered);
                return;
            }
            var algorithm = algorithms.CreateAlgorithm(symbol);

            if (algorithm.OrderAlgorithm.RejectRepeatCounter > 0) return;

            if (algorithm.OrderAlgorithm.ReceivedDesiredPosition)
            {
                if (algorithm.OrderAlgorithm.IsSynchronized)
                {
                    if (algorithm.OrderAlgorithm.IsBrokerOnline)
                    {
                        if (debug) log.DebugFormat("Attempted StartBroker but isBrokerStarted is already " + algorithm.OrderAlgorithm.IsBrokerOnline);
                        return;
                    }
                    if (debug) log.DebugFormat("Sending StartBroker for " + symbol + ". Reason: " + message);
                    TrySend(EventType.StartBroker, symbol, symbolReceiver.Agent);
                    algorithm.OrderAlgorithm.IsBrokerOnline = true;
                }
                else
                {
                    if (debug) log.DebugFormat("Attempted StartBroker but OrderAlgorithm not yet synchronized");
                }
            }
            else
            {
                TryRequestPosition(symbol);
            }
        }

        public void TrySendEndBroker() {
            foreach(var symbolReceiver in symbolReceivers.GetReceivers()) {
                TrySendEndBroker(symbolReceiver.Symbol);
            }
        }

        protected void TrySendEndBroker( SymbolInfo symbol)
        {
            var symbolReceiver = symbolReceivers.GetSymbolRequest(symbol);
            var algorithm = algorithms.CreateAlgorithm(symbol);
            if (!algorithm.OrderAlgorithm.IsBrokerOnline)
            {
                if (debug) log.DebugFormat("Tried to send EndBroker for " + symbol + " but broker status is already offline.");

                if( SyncTicks.Enabled)
                {
                    var tickSync = SyncTicks.GetTickSync(symbol.BinaryIdentifier);
                    if( tickSync.SentSwtichBrokerState)
                    {
                        tickSync.ClearSwitchBrokerState("already offline");
                    }
                }
                return;
            }
            var item = new EventItem(symbol, EventType.EndBroker);
            symbolReceiver.Agent.SendEvent(item);
            algorithm.OrderAlgorithm.IsBrokerOnline = false;
            if (debug) log.DebugFormat("Sent EndBroker for " + symbol + ".");
        }

        protected int GetOrderSide( OrderSide side) {
            switch( side) {
                case OrderSide.Buy:
                    return 1;
                case OrderSide.Sell:
                    return 2;
                case OrderSide.SellShort:
                    return 5;
                case OrderSide.SellShortExempt:
                    return 6;
                default:
                    throw new ApplicationException("Unknown OrderSide: " + side);
            }
        }

        public virtual void PositionChange(PositionChangeDetail positionChange)
        {
            if( debug) log.DebugFormat( "PositionChange " + positionChange);
            var algorithm = algorithms.CreateAlgorithm(positionChange.Symbol);
            if( algorithm.OrderAlgorithm.PositionChange(positionChange, IsRecovered))
            {
                TrySendStartBroker(positionChange.Symbol, "position change sync");
            }
        }

        protected int SideToSign( string side) {
            switch( side) {
                case "1": // Buy
                case "3": // Buy Minus
                    return 1;
                case "2": // Sell 
                case "4": // Sell Plus
                case "5": // Sell Minus
                case "6": // SellShort
                case "9": // CrossShort
                    return -1;
                default:
                    throw new ApplicationException("Unknown order side: " + side);
            }
        }

        public virtual void OnLogout()
        {
            if (isDisposed)
            {
                if (debug) log.DebugFormat("LimeProvider.OnLogOut() already disposed.");
                return;
            }
            var limeMessage = FixFactory.Create();
            limeMessage.AddHeader("5");
            SendMessage(limeMessage);
            log.Info("Logout message sent: " + limeMessage);
            log.Info("Logging out -- Sending EndBroker event.");
            TrySendEndBroker();
        }

        protected virtual void OnStartRecovery()
        {
            if( !LogRecovery) {
                MessageFIXT1_1.IsQuietRecovery = true;
            }
            CancelRecovered();
            TryEndRecovery();
        }

        protected virtual void OnFinishRecovery()
        {
        }

        protected abstract void HandleRejectedLogin(MessageFIXT1_1 message);

        private void OnConnect()
        {
            log.Info("OnConnect() ");
            ConnectionStatus = Status.Connected;
            if (SyncTicks.Enabled)
            {
                HeartbeatDelay = 10;
                if (HeartbeatDelay > 40)
                {
                    log.Error("Heartbeat delay is " + HeartbeatDelay);
                }
            }
            else
            {
                HeartbeatDelay = 40;
            }
            IsResendComplete = true;
            using (OrderStore.BeginTransaction())
            {
                if (OnLogin())
                {
                    ConnectionStatus = Status.PendingLogin;
                    IncreaseHeartbeatTimeout();
                }
                else
                {
                    SocketReconnect.Regenerate();
                }
            }
        }

        public virtual void OnDisconnect()
        {
            HeartbeatDelay = int.MaxValue;
            if( ConnectionStatus == Status.PendingLogOut)
            {
                if( debug) log.DebugFormat("Sending RemoteShutdown confirmation back to provider manager.");
            }
            else 
            {
                OrderStore.ForceSnapshot();
                var message = "MBTFIXProvider disconnected.";
                if (SyncTicks.Enabled)
                {
                    log.Notice(message);
                }
                else
                {
                    log.Error(message);
                }
                log.Info("Logging out -- Sending EndBroker event.");
                TrySendEndBroker();
            }
            switch (ConnectionStatus)
            {
                case Status.Connected:
                case Status.Disconnected:
                case Status.New:
                case Status.PendingRecovery:
                case Status.Recovered:
                case Status.PendingLogin:
                case Status.PendingServerResend:
                case Status.PendingRetry:
                    orderStore.ForceSnapshot();
                    ConnectionStatus = Status.Disconnected;
                    SocketReconnect.Regenerate();
                    break;
                case Status.PendingLogOut:
                    ConnectionStatus = Status.Disconnected;
                    Dispose();
                    break;
                default:
                    log.Warn("Unexpected connection status when socket disconnected: " + ConnectionStatus + ". Shutting down the provider.");
                    Dispose();
                    break;
            }
        }

        public abstract bool OnCreateBrokerOrder(CreateOrChangeOrder createOrChangeOrder);
        public abstract bool OnCancelBrokerOrder(CreateOrChangeOrder order);
        public abstract bool OnChangeBrokerOrder(CreateOrChangeOrder createOrChangeOrder);

        public int ProcessOrders()
        {
            return 0;
        }

        public virtual void ProcessFill(SymbolInfo symbol, LogicalFillBinary fill)
        {
            var symbolReceiver = symbolReceivers.GetSymbolRequest(symbol);
            var symbolAlgorithm = algorithms.CreateAlgorithm(symbol);
            if( !symbolAlgorithm.OrderAlgorithm.IsBrokerOnline)
            {
                if (debug) log.DebugFormat("Broker offline but sending fill anyway for " + symbol + " to receiver: " + fill);
            }
            if (debug) log.DebugFormat("Sending fill event for " + symbol + " to receiver: " + fill);
            var item = new EventItem(symbol, EventType.LogicalFill, fill);
            symbolReceiver.Agent.SendEvent(item);
        }

        public virtual void ProcessTouch(SymbolInfo symbol, LogicalTouch touch)
        {
            var symbolReceiver = symbolReceivers.GetSymbolRequest(symbol);
            var symbolAlgorithm = algorithms.CreateAlgorithm(symbol);
            if (!symbolAlgorithm.OrderAlgorithm.IsBrokerOnline)
            {
                if (debug) log.DebugFormat("Broker offline so not sending logical touch for " + symbol + ": " + touch);
                if (SyncTicks.Enabled)
                {
                    var tickSync = SyncTicks.GetTickSync(symbol.BinaryIdentifier);
                    tickSync.RemovePhysicalFill(touch);
                }
                return;
            }
            if (debug) log.DebugFormat("Sending logical touch for " + symbol + " to receiver for logical touch: " + touch);
            var item = new EventItem(symbol, EventType.LogicalTouch, touch);
            symbolReceiver.Agent.SendEvent(item);
        }

        protected void HandleCancelReject(string clientOrderIdStr, string text, MessageFIXT1_1 packetFIX)
        {
            var clientOrderId = 0L;
            long.TryParse(clientOrderIdStr, out clientOrderId);
            var originalClientOrderId = 0L;
            long.TryParse(clientOrderIdStr, out originalClientOrderId);

            CreateOrChangeOrder order;
            if (OrderStore.TryGetOrderById(clientOrderId, out order))
            {
                var symbol = order.Symbol;
                var algorithm = algorithms.CreateAlgorithm(symbol);
                var orderAlgo = algorithm.OrderAlgorithm;
                if (IsRecovered && orderAlgo.RejectRepeatCounter > 0 && orderAlgo.IsBrokerOnline)
                {
                    var message = "Cancel Rejected on " + symbol + ": " + text + "\n" + packetFIX;
                    log.Error(message);
                }

                var retryImmediately = algorithm.OrderAlgorithm.RejectRepeatCounter == 0;
                algorithm.OrderAlgorithm.RejectOrder(clientOrderId, IsRecovered, retryImmediately);
                if (!retryImmediately)
                {
                    TrySendEndBroker(symbol);
                }
            }
            else
            {
                if (debug) log.DebugFormat("Order not found for " + clientOrderId + ". Probably allready filled or canceled.");
            }
        }

        protected void HandleOrderReject(string clientOrderIdStr, string symbolStr, string text, MessageFIXT1_1 packetFix)
        {
            var clientOrderId = 0L;
            long.TryParse(clientOrderIdStr, out clientOrderId);
            var originalClientOrderId = 0L;
            long.TryParse(clientOrderIdStr, out originalClientOrderId);
            SymbolInfo symbolInfo;
            var symbolString = string.IsNullOrEmpty(SymbolSuffix) ? symbolStr : symbolStr.Replace(SymbolSuffix, "");
            if (!Factory.Symbol.TryLookupSymbol(symbolString, out symbolInfo))
            {
                log.Warn("Unable to find " + symbolStr + " for order reject.");
                return;
            }
            var algorithm = algorithms.CreateAlgorithm(symbolInfo);
            var orderAlgo = algorithm.OrderAlgorithm;
            if (IsRecovered && orderAlgo.RejectRepeatCounter > 0 && orderAlgo.IsBrokerOnline)
            {
                var message = "Order Rejected on " + symbolInfo + ": " + text + "\n" + packetFix;
                if( Factory.IsAutomatedTest)
                {
                    log.Notice(message);
                }
                else
                {
                    log.Error(message);
                }
            }

            var retryImmediately = algorithm.OrderAlgorithm.RejectRepeatCounter == 0;
            algorithm.OrderAlgorithm.RejectOrder(clientOrderId, IsRecovered, retryImmediately);
            if( !retryImmediately)
            {
                TrySendEndBroker(symbolInfo);
            }
        }

        protected void HandleBusinessReject(bool errorOkay, MessageFIXT1_1 packetFIX)
        {
            var text = packetFIX.Text;
            CancelRecovered();
            TrySendEndBroker();
            TryEndRecovery();
            log.Info(text + " Sent EndBroker event due to Message:\n" + packetFIX);
            long clientOrderId;
            if (packetFIX.BusinessRejectReferenceId != null && long.TryParse(packetFIX.BusinessRejectReferenceId, out clientOrderId))
            {
                CreateOrChangeOrder order;
                if (OrderStore.TryGetOrderById(clientOrderId, out order))
                {
                    var symbol = order.Symbol;
                    var algorithm = algorithms.CreateAlgorithm(symbol);
                    var orderAlgo = algorithm.OrderAlgorithm;
                    if (IsRecovered && orderAlgo.RejectRepeatCounter > 0 && orderAlgo.IsBrokerOnline)
                    {
                        var message = "Business reject on " + symbol + ": " + packetFIX.Text + "\n" + packetFIX;
                        log.Error(message);
                    }

                    var retryImmediately = algorithm.OrderAlgorithm.RejectRepeatCounter == 0;
                    algorithm.OrderAlgorithm.RejectOrder(clientOrderId, IsRecovered, retryImmediately);
                    if (!retryImmediately)
                    {
                        TrySendEndBroker(symbol);
                    }
                }
                else
                {
                    if (debug) log.DebugFormat("Order not found for " + clientOrderId + ". Probably allready filled or canceled.");
                }
            }
            else if (!errorOkay)
            {
                string message = "FIX Server reported an error: " + packetFIX.Text + "\n" + packetFIX;
                throw new ApplicationException( message);
            }
        }

        protected virtual void SendHeartbeat()
        {
            if (debug) log.DebugFormat("SendHeartBeat Status " + ConnectionStatus + ", Session Status Online " + isOrderServerOnline + ", Resend Complete " + IsResendComplete);
            if (!IsRecovered) TryEndRecovery();
            if (IsRecovered)
            {
                foreach( var algo in algorithms.GetAlgorithms())
                {
                    algo.OrderAlgorithm.RejectRepeatCounter = 0;
                    if( !algo.OrderAlgorithm.CheckForPending())
                    {
                        algo.OrderAlgorithm.ProcessOrders();
                    }
                }
            }
            var fixMsg = FixFactory.Create();
            fixMsg.AddHeader("0");
            SendMessage( fixMsg);
            previousHeartbeatTime = recentHeartbeatTime;
            recentHeartbeatTime = TimeStamp.UtcNow;
        }

        protected string GetOpenOrders()
        {
            var sb = new StringBuilder();
            var list = OrderStore.GetOrders((x) => true);
            foreach( var order in list) {
                sb.Append("    ");
                sb.Append( order.BrokerOrder);
                sb.Append(" ");
                sb.Append(order);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        protected abstract void SendLogin(int localSequence, bool b);

        public virtual bool OnLogin()
        {
            if (debug) log.DebugFormat("LimeFIXProvider.Login()");

            if (OrderStore.Recover())
            {
                // Reset the order algorithms
                algorithms.Reset();
                var synthetics = OrderStore.GetOrders((o) => o.IsSynthetic);
                foreach (var order in synthetics)
                {
                    var algo = algorithms.CreateAlgorithm(order.Symbol);
                    switch( order.Action)
                    {
                        case OrderAction.Create:
                            algo.Synthetics.OnCreateBrokerOrder(order);
                            break;
                        case OrderAction.Change:
                            algo.Synthetics.OnChangeBrokerOrder(order);
                            break;
                        case OrderAction.Cancel:
                            algo.Synthetics.OnCancelBrokerOrder(order);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                if (debug) log.DebugFormat("Recovered from snapshot Local Sequence " + OrderStore.LocalSequence + ", Remote Sequence " + OrderStore.RemoteSequence);
                if (debug) log.DebugFormat("Recovered orders from snapshot: \n" + OrderStore.OrdersToString());
                if (debug) log.DebugFormat("Recovered symbol positions from snapshot:\n" + OrderStore.SymbolPositionsToString());
                if (debug) log.DebugFormat("Recovered strategy positions from snapshot:\n" + OrderStore.StrategyPositionsToString());
                RemoteSequence = OrderStore.RemoteSequence;
                SendLogin(OrderStore.LocalSequence,false);
                OrderStore.RequestSnapshot();
            }
            else
            {
                if (debug) log.DebugFormat("Unable to recover from snapshot. Beginning full recovery.");
                OrderStore.SetSequences(0, 0);
                OrderStore.ForceSnapshot();
                SendLogin(OrderStore.LocalSequence,true);
            }
            return true;
        }

        public virtual void OnStartBroker(SymbolInfo symbol)
        {
            var algorithm = algorithms.CreateAlgorithm(symbol);
            if (ConnectionStatus == Status.Recovered)
            {
                algorithm.OrderAlgorithm.ProcessOrders();
                TrySendStartBroker(symbol,"Start broker");
            }
        }

        protected void StartPositionSync()
        {
            if (debug) log.DebugFormat("StartPositionSync()");
            var openOrders = GetOpenOrders();
            if (string.IsNullOrEmpty(openOrders))
            {
                if (debug) log.DebugFormat("Found 0 open orders prior to sync.");
            }
            else
            {
                if (debug) log.DebugFormat("Orders prior to sync:\n" + openOrders);
            }
            MessageFIXT1_1.IsQuietRecovery = false;
            foreach (var algorithm in algorithms.GetAlgorithms())
            {
                var symbol = algorithm.OrderAlgorithm.Symbol;
                algorithm.OrderAlgorithm.ProcessOrders();
                TrySendStartBroker(symbol, "start position sync");
            }
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        protected virtual void BusinessReject(MessageFIXT1_1 packetFIX)
        {
            HandleBusinessReject(false, packetFIX);
        }
        public Status ConnectionStatus
        {
            get { return connectionStatus; }
            set
            {
                if (connectionStatus != value)
                {
                    if (debug) log.DebugFormat("ConnectionStatus changed from " + connectionStatus + " to " + value);
                    connectionStatus = value;
                }
            }
        }

        public bool DisableChangeOrders
        {
            get { return disableChangeOrders; }
        }

        public string SymbolSuffix
        {
            get { return symbolSuffix; }
        }
    }
}