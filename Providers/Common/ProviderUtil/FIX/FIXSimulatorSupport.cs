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
using System.Diagnostics;
using System.Text;
using System.Threading;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public abstract class FIXSimulatorSupport : FIXSimulator, LogAware
	{
		private string localAddress = "sharedmemory";
		private static Log log = Factory.SysLog.GetLogger(typeof(FIXSimulatorSupport));
        private volatile bool debug;
        private volatile bool trace;
        private volatile bool verbose;
        public virtual void RefreshLogLevel()
        {
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
            verbose = log.IsVerboseEnabled;
        }

        private ProviderSimulatorSupport providerSimulator;
        protected FIXTFactory1_1 fixFactory;
		private long realTimeOffset;
		private object realTimeOffsetLocker = new object();
		private YieldMethod MainLoopMethod;
        private int heartbeatDelay = 10;
        //private int heartbeatDelay = int.MaxValue;
        private ServerState fixState = ServerState.Startup;
        private readonly int maxFailures = 5;
        private bool allTests;
        private bool simulateReceiveFailed;
        private bool simulateSendFailed;

        private bool isConnectionLost = false;

		// FIX fields.
		private ushort fixPort = 0;
		private Socket fixListener;
		protected Socket fixSocket;
		private Message _fixReadMessage;
		private Message _fixWriteMessage;
		private Task task;
		private bool isFIXSimulationStarted = false;
        private MessageFactory currentMessageFactory;
		private FastQueue<Message> fixPacketQueue = Factory.Parallel.FastQueue<Message>("SimulatorFIX");
        private QueueFilter filter;
        private int frozenHeartbeatCounter;

        private TrueTimer heartbeatTimer;
        private TimeStamp heartbeatResponseDeadline = TimeStamp.MaxValue;
        private Agent agent;
        private bool isResendComplete = true;
        public Agent Agent
        {
            get { return agent; }
            set { agent = value; }
        }

        public Dictionary<SimulatorType,SimulatorInfo> simulators = new Dictionary<SimulatorType, SimulatorInfo>();

        public FIXSimulatorSupport(string mode, ProjectProperties projectProperties, ProviderSimulatorSupport providerSimulator, ushort fixPort, MessageFactory createMessageFactory)
        {
		    this.fixPort = fixPort;
            this.providerSimulator = providerSimulator;
		    var randomSeed = new Random().Next(int.MaxValue);
            if (heartbeatDelay > 10)
            {
                log.Error("Heartbeat delay is " + heartbeatDelay);
            }

		    if (randomSeed != 1234)
		    {
		        Console.WriteLine("Random seed for fix simulator:" + randomSeed);
		        log.Info("Random seed for fix simulator:" + randomSeed);
		    }
		    random = new Random(randomSeed);
		    log.Register(this);
		    switch (mode)
		    {
		        case "PlayBack":
		            break;
                default:
		            break;
		    }

            allTests = projectProperties.Simulator.EnableNegativeTests;

            foreach (SimulatorType simulatorType in Enum.GetValues(typeof(SimulatorType)))
            {
                var simulator = new SimulatorInfo(simulatorType, random, () => ProviderSimulator.Count);
                simulator.Enabled = false;
                simulator.MaxFailures = maxFailures;
                simulators.Add(simulatorType, simulator);
            }
            simulators[SimulatorType.BlackHole].Enabled = false;   // Black hole testing no longer supported.
            simulators[SimulatorType.CancelBlackHole].Enabled = false;   // Black hole testing no longer supported.
            simulators[SimulatorType.RejectSymbol].Enabled = allTests;   // Passed individually.
            simulators[SimulatorType.RejectAll].Enabled = false;  // Not implemented
            simulateReceiveFailed = allTests;    // Passed individually.
            simulateSendFailed = allTests;      // Passed individually.
            simulators[SimulatorType.SendServerOffline].Enabled = allTests; // Passed individually.
            simulators[SimulatorType.ReceiveServerOffline].Enabled = allTests;  // Passed individually.
            simulators[SimulatorType.ServerOfflineReject].Enabled = allTests;  // Passed individually.
            simulators[SimulatorType.SendDisconnect].Enabled = allTests;        // Passed individually.
            simulators[SimulatorType.ReceiveDisconnect].Enabled = allTests;     // Passed individually.
            simulators[SimulatorType.SystemOffline].Enabled = allTests;     // Passed individually.

            {
                simulators[SimulatorType.ReceiveServerOffline].Frequency = 10;
                simulators[SimulatorType.SendServerOffline].Frequency = 20;
                simulators[SimulatorType.SendDisconnect].Frequency = 15;
                simulators[SimulatorType.ReceiveDisconnect].Frequency = 8;
                simulators[SimulatorType.CancelBlackHole].Frequency = 10;
                simulators[SimulatorType.BlackHole].Frequency = 10;
                simulators[SimulatorType.SystemOffline].Frequency = 10;

                simulators[SimulatorType.ServerOfflineReject].Frequency = 10;

                var simulator = simulators[SimulatorType.RejectSymbol];
                simulator.Frequency = 10;
                simulator.MaxRepetitions = 1;
            }

            foreach( var kvp in projectProperties.Simulator.NegativeSimulatorMinimums)
            {
                var type = kvp.Key;
                var minimum = kvp.Value;
                if( minimum == 0)
                {
                    simulators[type].Minimum = minimum;
                    simulators[type].Enabled = false;
                }
                else
                {
                    simulators[type].Minimum = minimum;
                }
            }

            this.currentMessageFactory = createMessageFactory;
        }

        public void Initialize(Task task)
        {
            this.task = task;
            filter = task.GetFilter();
            task.Scheduler = Scheduler.EarliestTime;
            fixPacketQueue.ConnectInbound(task);
            heartbeatTimer = Factory.Parallel.CreateTimer("Heartbeat", task, HeartbeatTimerEvent);
            IncreaseHeartbeat();
            task.Start();
            ListenToFIX();
            MainLoopMethod = Invoke;
            if (debug) log.DebugFormat(LogMessage.LOGMSG180);
            if (allTests)
            {
                foreach( var kvp in simulators)
                {
                    var simulator = kvp.Value;
                    if( !simulator.Enabled && simulator.Minimum > 0)
                    {
                        log.Error(simulator + " is disabled");
                    }
                }
                if (!simulateReceiveFailed)
                {
                    log.Error("SimulateReceiveFailed is disabled.");
                }
                if (!simulateSendFailed)
                {
                    log.Error("SimulateSendFailed is disabled.");
                }
            }
        }

        public void DumpHistory()
        {
            for (var i = 0; i <= FixFactory.LastSequence; i++)
            {
                FIXTMessage1_1 message;
                if( FixFactory.TryGetHistory(i, out message))
                {
                    log.Info(message.ToString());
                }
            }
        }

        private void ListenToFIX()
		{
            fixListener = Factory.Provider.Socket(typeof(FIXSimulatorSupport).Name, localAddress, fixPort);
			fixListener.Bind();
			fixListener.Listen( 5);
			fixListener.OnConnect = OnConnectFIX;
			fixPort = fixListener.Port;
			log.Info("Listening for FIX to " + localAddress + " on port " + fixPort);
		}

		protected virtual void OnConnectFIX(Socket socket)
		{
			fixSocket = socket;
            fixState = ServerState.Startup;
		    fixSocket.MessageFactory = currentMessageFactory;
			log.Info("Received FIX connection: " + socket);
			StartFIXSimulation();
			fixSocket.ReceiveQueue.ConnectInbound( task);
            fixSocket.SendQueue.ConnectOutbound(task);
		}

        private Yield HeartbeatTimerEvent()
        {
            if (isDisposed) return Yield.Terminate;
            var currentTime = TimeStamp.UtcNow;
            if (verbose) log.VerboseFormat(LogMessage.LOGMSG252, currentTime);
            IncreaseHeartbeat();
            if (isConnectionLost)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG253);
                if (!fixPacketQueue.IsEmpty && FIXReadLoop())
                {
                    return Yield.DidWork.Repeat;
                }
                ProviderSimulator.SwitchBrokerState("heartbeat-connectionlost", false);
                CloseFIXSocket();
                return Yield.NoWork.Repeat;
            }
            switch( fixState)
            {
                case ServerState.Startup:
                case ServerState.ServerResend:
                    if (debug) log.DebugFormat(LogMessage.LOGMSG254, fixState);
                    break;
                case ServerState.WaitingHeartbeat:
                    if (currentTime > heartbeatResponseDeadline)
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG255);
                        isConnectionLost = true;
                        return Yield.DidWork.Repeat;
                    }
                    return Yield.NoWork.Repeat;
                case ServerState.Recovered:
                    if (SyncTicks.Frozen)
                    {
                        frozenHeartbeatCounter++;
                        if (frozenHeartbeatCounter > 3)
                        {
                            if (debug) log.DebugFormat(LogMessage.LOGMSG198);
                            heartbeatDelay = 1000;
                        }
                        else
                        {
                            OnHeartbeat();
                        }
                    }
                    else
                    {
                        OnHeartbeat();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unexpected FIX state of " + fixState);
            }
            return Yield.DidWork.Repeat;
        }

        private void OnDisconnectFIX(Socket socket)
		{
			if (this.fixSocket.Equals(socket)) {
				log.Info("FIX socket disconnect: " + socket);
                StopFIXSimulation();
                CloseFIXSocket();
			}
		}

        protected virtual void StopFIXSimulation()
        {
            isFIXSimulationStarted = false;
            if( resetSequenceNumbersNextDisconnect)
            {
                resetSequenceNumbersNextDisconnect = false;
                if (debug) log.DebugFormat(LogMessage.LOGMSG256);
                remoteSequence = 1;
                fixFactory = CreateFIXFactory(0, FixFactory.Sender, FixFactory.Destination);
            }
        }

        protected void CloseFIXSocket()
        {
            if (fixPacketQueue != null && fixPacketQueue.Count > 0)
            {
                fixPacketQueue.Clear();
            }
            if (fixSocket != null)
            {
                fixSocket.Dispose();
            }
        }

		protected void CloseSockets()
		{
            if (task != null)
            {
                task.Stop();
                task.Join();
            }
            CloseFIXSocket();
		}

		public virtual void StartFIXSimulation()
		{
			isFIXSimulationStarted = true;
            isConnectionLost = false;
        }

        public void Shutdown()
        {
            Dispose();
        }

		private enum State { Start, ProcessFIX, WriteFIX, Return };
		private State state = State.Start;
		private bool hasFIXPacket;
		public Yield Invoke()
		{
            if( isConnectionLost)
            {
                if (!fixPacketQueue.IsFull && FIXReadLoop())
                {
                    return Yield.DidWork.Repeat;
                }
                ProviderSimulator.SwitchBrokerState("invoke-connectionlost", false);
                CloseFIXSocket();
                return Yield.NoWork.Repeat;
            }
			var result = false;
			switch( state) {
				case State.Start:
					if( !fixPacketQueue.IsFull && FIXReadLoop())
					{
						result = true;
					}
				ProcessFIX:
					hasFIXPacket = ProcessFIXPackets();
					if( hasFIXPacket ) {
						result = true;
					}
				WriteFIX:
					if( hasFIXPacket) {
						if( !WriteToFIX()) {
							state = State.WriteFIX;
							return Yield.NoWork.Repeat;
						}
                        //if( fixPacketQueue.Count > 0) {
                        //    state = State.ProcessFIX;
                        //    return Yield.DidWork.Repeat;
                        //}
					}
			        break;
				case State.ProcessFIX:
					goto ProcessFIX;
				case State.WriteFIX:
					goto WriteFIX;
			}
			state = State.Start;
			if( result) {
				return Yield.DidWork.Repeat;
			} else {
				return Yield.NoWork.Repeat;
			}
		}

		private bool ProcessFIXPackets() {
			if( _fixWriteMessage == null && fixPacketQueue.Count == 0) {
				return false;
			}
			if( trace) log.TraceFormat(LogMessage.LOGMSG257, fixPacketQueue.Count);
			if( fixPacketQueue.TryDequeue(out _fixWriteMessage)) {
				return true;
			} else {
				return false;
			}
		}

        private FIXTMessage1_1 GapFillMessage(int currentSequence)
        {
            var message = FixFactory.Create(currentSequence);
            message.SetGapFill();
            message.SetNewSeqNum(currentSequence + 1);
            message.AddHeader("4");
            return message;
        }

        private bool HandleResend(MessageFIXT1_1 messageFIX)
        {
            int end = messageFIX.EndSeqNum == 0 ? FixFactory.LastSequence : messageFIX.EndSeqNum;
            if (debug) log.DebugFormat(LogMessage.LOGMSG219, messageFIX.BegSeqNum, end, messageFIX);
            var sentTestRequest = false;
            var sentHeartbeat = false;
            for (int i = messageFIX.BegSeqNum; i <= end; i++)
            {
                FIXTMessage1_1 textMessage;
                var gapFill = false;
                if (!FixFactory.TryGetHistory(i, out textMessage))
                {
                    gapFill = true;
                    textMessage = GapFillMessage(i);
                }
                else
                {
                    switch (textMessage.Type)
                    {
                        case "A": // Logon
                        case "2": // Resend request.
                        case "4": // Reset sequence.
                        case "5": // Reset sequence.
                            textMessage = GapFillMessage(i);
                            gapFill = true;
                            break;
                        case "1": // Test Request
                            if( sentTestRequest)
                            {
                                gapFill = true;
                            }
                            else
                            {
                                textMessage.SetDuplicate(true);
                                sentTestRequest = true;
                            }
                            break;
                        case "0": // Heart beat
                            if (sentHeartbeat)
                            {
                                gapFill = true;
                            }
                            else
                            {
                                textMessage.SetDuplicate(true);
                                sentHeartbeat = true;
                            }
                            break;
                        default:
                            textMessage.SetDuplicate(true);
                            break;
                    }

                }

                if (gapFill)
                {
                    if (debug)
                    {
                        var fixString = textMessage.ToString();
                        string view = fixString.Replace(FIXTBuffer.EndFieldStr, "  ");
                        log.DebugFormat(LogMessage.LOGMSG258, i, view);
                    }
                    ResendMessageProtected(textMessage);
                }
                else
                {
                    ResendMessage(textMessage);
                }
            }
            return true;
        }

        protected abstract void ResendMessage(FIXTMessage1_1 textMessage);
        protected abstract void RemoveTickSync(MessageFIXT1_1 textMessage);
        protected abstract void RemoveTickSync(FIXTMessage1_1 textMessage);

        public bool TrySendSessionStatus(string status)
        {
            switch( status)
            {
                case "2":
                    ProviderSimulator.SetOrderServerOnline();
                    break;
                case "3":
                    ProviderSimulator.SetOrderServerOffline();
                    break;
                default:
                    throw new ApplicationException("Unknown session status:" + status);
            }
            if (requestSessionStatus)
            {
                var mbtMsg = FixFactory.Create();
                mbtMsg.AddHeader("h");
                mbtMsg.SetTradingSessionId("TSSTATE");
                mbtMsg.SetTradingSessionStatus(status);
                if (debug) log.DebugFormat(LogMessage.LOGMSG259, mbtMsg);
                SendMessage(mbtMsg);
            }
            else
            {
                log.Info("RequestSessionStatus is false so not sending order server offline message.");
            }
            return true;
        }

        private bool Resend(MessageFIXT1_1 messageFix)
		{
            if( !isResendComplete) return true;
			var mbtMsg = FixFactory.Create();
			mbtMsg.AddHeader("2");
			mbtMsg.SetBeginSeqNum(RemoteSequence);
			mbtMsg.SetEndSeqNum(0);
			if( debug) log.DebugFormat(LogMessage.LOGMSG260, mbtMsg);
            SendMessage(mbtMsg);
		    return true;
		}

        private Random random;
		private int remoteSequence = 1;
        private int recoveryRemoteSequence = 1;
		private bool FIXReadLoop()
		{
            var result = false;
            if (isFIXSimulationStarted)
			{
			    Message message;
                if (fixSocket.TryGetMessage(out message))
                {
                    var disconnect = message as DisconnectMessage;
                    if (disconnect == null)
                    {
                        _fixReadMessage = message;
                    }
                    else
                    {
                        OnDisconnectFIX(disconnect.Socket);
                        return false;
                    }
                }
                else
                {
                    return false;
                }
			    var packetFIX = (MessageFIXT1_1)_fixReadMessage;
                if (debug) log.DebugFormat(LogMessage.LOGMSG261, packetFIX);
			    heartbeatResponseDeadline = TimeStamp.MaxValue;
                try
                {
                    switch( fixState)
                    {
                        case ServerState.Startup:
                            if (packetFIX.MessageType != "A")
                            {
                                throw new InvalidOperationException("Invalid FIX message type " + packetFIX.MessageType + ". Not yet logged in.");
                            }
                            if (!packetFIX.IsResetSeqNum && packetFIX.Sequence < RemoteSequence)
                            {
                                var loginReject = "MsgSeqNum too low, expecting " + RemoteSequence + " but received " + packetFIX.Sequence;
                                SendLoginReject(loginReject);
                                return true;
                            }
                            HandleFIXLogin(packetFIX);
                            if (packetFIX.Sequence > RemoteSequence)
                            {
                                fixState = ServerState.ServerResend;
                                if (debug) log.DebugFormat(LogMessage.LOGMSG262, packetFIX.Sequence, RemoteSequence);
                                recoveryRemoteSequence = packetFIX.Sequence;
                                return Resend(packetFIX);
                            }
                            else
                            {
                                if (debug) log.DebugFormat(LogMessage.LOGMSG263, packetFIX.Sequence, RemoteSequence);
                                RemoteSequence = packetFIX.Sequence + 1;
                                ServerSyncComplete();
                            }
                            break;
                        case ServerState.ServerResend:
                        case ServerState.Recovered:
                        case ServerState.WaitingHeartbeat:
                            switch (packetFIX.MessageType)
                            {
                                case "A":
                                    throw new InvalidOperationException("Invalid FIX message type " + packetFIX.MessageType + ". Already logged in.");
                            }
                            if (packetFIX.Sequence > RemoteSequence)
                            {
                                if (debug) log.DebugFormat(LogMessage.LOGMSG264, packetFIX.Sequence, RemoteSequence);
                                return Resend(packetFIX);
                            }
                            if (packetFIX.Sequence < RemoteSequence)
                            {
                                if (debug) log.DebugFormat(LogMessage.LOGMSG265, packetFIX.Sequence);
                                return true;
                            }
                            switch (packetFIX.MessageType)
                            {
                                case "2":
                                    if (fixState == ServerState.ServerResend)
                                    {
                                        OnBusinessReject("Client must respond to resend request of server before submitting any resend requests.");
                                        return true;
                                    }
                                    HandleResend(packetFIX);
                                    if (debug) log.DebugFormat(LogMessage.LOGMSG266, packetFIX.Sequence);
                                    RemoteSequence = packetFIX.Sequence + 1;
                                    break;
                                case "4":
                                    result = HandleGapFill(packetFIX);
                                    break;
                                default:
                                    result = ProcessMessage(packetFIX);
                                    break;
                            }
                            if (RemoteSequence >= recoveryRemoteSequence)
                            {
                                isResendComplete = true;
                                if (fixState == ServerState.ServerResend)
                                {
                                    // Sequences are synchronized now. Send TradeSessionStatus.
                                    ServerSyncComplete();
                                }
                            }
                            break;
                    }
                }
                finally
                {
                    fixSocket.MessageFactory.Release(_fixReadMessage);
                }
			}
			return result;
		}

        protected bool requestSessionStatus;

        private bool resetSequenceNumbersNextDisconnect;
        private void SendSystemOffline()
        {
            var mbtMsg = FixFactory.Create();
            mbtMsg.AddHeader("5");
            mbtMsg.SetText("System offline");
            SendMessage(mbtMsg);
            if (trace) log.TraceFormat(LogMessage.LOGMSG267, mbtMsg);
            //resetSequenceNumbersNextDisconnect = true;
        }

        private void SendLoginReject(string message)
        {
            var mbtMsg = FixFactory.Create();
            mbtMsg.AddHeader("5");
            mbtMsg.SetText(message);
            SendMessage(mbtMsg);
            if (trace) log.TraceFormat(LogMessage.LOGMSG268, mbtMsg);
        }

        private void ServerSyncComplete()
        {
            fixState = ServerState.Recovered;
            SendServerSyncComplete();
            SendSessionStatusOnline();
            ProviderSimulator.FlushFillQueues();
            // Setup disconnect simulation.
            simulators[SimulatorType.SendDisconnect].UpdateNext(FixFactory.LastSequence);
        }

        public virtual void SendServerSyncComplete()
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG269);
        }

        public virtual void SendSessionStatusOnline()
        {
            if (debug) log.DebugFormat(LogMessage.LOGMSG270);
            var wasOrderServerOnline = ProviderSimulator.IsOrderServerOnline;
            TrySendSessionStatus("2");
            if( !wasOrderServerOnline)
            {
                ProviderSimulator.SwitchBrokerState("online",true);
            }            
            ProviderSimulator.FlushFillQueues();
        }


        protected bool HandleFIXLogin(MessageFIXT1_1 packet)
        {
            if (fixState != ServerState.Startup)
            {
                throw new InvalidOperationException("Invalid login request. Already logged in: \n" + packet);
            }

            SetupFixFactory(packet);

            simulators[SimulatorType.SendDisconnect].UpdateNext(FixFactory.LastSequence);
            simulators[SimulatorType.ReceiveDisconnect].UpdateNext(packet.Sequence);
            simulators[SimulatorType.SendServerOffline].UpdateNext(FixFactory.LastSequence);
            simulators[SimulatorType.ReceiveServerOffline].UpdateNext(packet.Sequence);
            simulators[SimulatorType.SystemOffline].UpdateNext(packet.Sequence);

            var mbtMsg = CreateLoginResponse();
            if (debug) log.DebugFormat(LogMessage.LOGMSG271, mbtMsg);
            SendMessage(mbtMsg);
            return true;
        }

        protected virtual void SetupFixFactory(MessageFIXT1_1 packet) {
            if (packet.IsResetSeqNum) {
                if (packet.Sequence != 1) {
                    throw new InvalidOperationException("Found reset sequence number flag is true but sequence was " +
                                                        packet.Sequence + " instead of 1.");
                }
                fixFactory = CreateFIXFactory(1, packet.Target, packet.Sender);
                if (debug) log.DebugFormat(LogMessage.LOGMSG272, fixFactory.LastSequence);
                RemoteSequence = packet.Sequence;
            }
            else if (FixFactory == null)
            {
                throw new InvalidOperationException(
                    "FIX login message specified tried to continue but simulator has no sequence history.");
            }
        }

        private FIXTMessage1_1 CreateLoginResponse()
        {
            var mbtMsg = (FIXTMessage1_1)FixFactory.Create();
            mbtMsg.SetEncryption(0);
            mbtMsg.SetHeartBeatInterval(HeartbeatDelay);
            mbtMsg.AddHeader("A");
            mbtMsg.SetSendTime(new TimeStamp(1800, 1, 1));
            return mbtMsg;
        }

        protected virtual FIXTFactory1_1 CreateFIXFactory(int sequence, string target, string sender)
        {
            throw new NotImplementedException();
        }

	    private bool HandleGapFill( MessageFIXT1_1 packetFIX)
        {
            if (!packetFIX.IsGapFill)
            {
                throw new InvalidOperationException("Only gap fill sequence reset supportted: \n" + packetFIX);
            }
            if (packetFIX.NewSeqNum <= RemoteSequence)  // ResetSeqNo
            {
                throw new InvalidOperationException("Reset new sequence number must be greater than current sequence: " + RemoteSequence + ".\n" + packetFIX);
            }
            RemoteSequence = packetFIX.NewSeqNum;
            if (debug) log.DebugFormat(LogMessage.LOGMSG206, RemoteSequence);
            return true;
        }

        private bool ProcessMessage(MessageFIXT1_1 packetFIX)
        {
            if( isConnectionLost)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG273, packetFIX);
                RemoveTickSync(packetFIX);
                return true;
            }
            var simulator = simulators[SimulatorType.ReceiveDisconnect];
            if (FixFactory != null && simulator.CheckSequence(packetFIX.Sequence))
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG274, packetFIX);
                // Ignore this message. Pretend we never received it AND disconnect.
                // This will test the message recovery.)
                ProviderSimulator.SwitchBrokerState("disconnect",false);
                isConnectionLost = true;
                return true;
            }
            if (simulateReceiveFailed && FixFactory != null && random.Next(50) == 1)
            {
                // Ignore this message. Pretend we never received it.
                // This will test the message recovery.
                if (debug) log.DebugFormat(LogMessage.LOGMSG275, packetFIX.Sequence);
                return Resend(packetFIX);
            }
            simulator = simulators[SimulatorType.ReceiveServerOffline];
            if (IsRecovered && FixFactory != null && simulator.CheckSequence(packetFIX.Sequence))
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG276, packetFIX);
                ProviderSimulator.SwitchBrokerState("disconnect", false);
                ProviderSimulator.SetOrderServerOffline();
                TrySendSessionStatus("3"); //offline
                return true;
            }

            simulator = simulators[SimulatorType.SystemOffline];
            if (IsRecovered && FixFactory != null && simulator.CheckSequence(packetFIX.Sequence))
            {
                SendSystemOffline();
                return true;
            }

            if (debug) log.DebugFormat(LogMessage.LOGMSG277, packetFIX.Sequence);
            RemoteSequence = packetFIX.Sequence + 1;
            var blackHoleEnabled = false;
            switch (packetFIX.MessageType)
            {
                case "G":
                case "D":
                    simulator = simulators[SimulatorType.BlackHole];
                    blackHoleEnabled = true;
                    break;
                case "F":
                    simulator = simulators[SimulatorType.CancelBlackHole];
                    blackHoleEnabled = true;
                    break;
            }
            if (blackHoleEnabled && FixFactory != null && simulator.CheckFrequency())
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG278, packetFIX.MessageType, RemoteSequence, packetFIX.Sequence);
                return true;
            }
            ParseFIXMessage(_fixReadMessage);
            return true;
        }


	    public long GetRealTimeOffset( long utcTime) {
			lock( realTimeOffsetLocker) {
				if( realTimeOffset == 0L) {
					var currentTime = TimeStamp.UtcNow;
					var tickUTCTime = new TimeStamp(utcTime);
				   	log.Info("First historical playback tick UTC tick time is " + tickUTCTime);
				   	log.Info("Current tick UTC time is " + currentTime);
				   	realTimeOffset = currentTime.Internal - utcTime;
				   	var microsecondsInMinute = 1000L * 1000L * 60L;
                    var extra = realTimeOffset % microsecondsInMinute;
                    realTimeOffset -= extra;
                    realTimeOffset += microsecondsInMinute;
				   	var elapsed = new Elapsed( realTimeOffset);
				   	log.Info("Setting real time offset to " + elapsed);
				}
			}
			return realTimeOffset;
		}

		public virtual void ParseFIXMessage(Message message)
		{
		}

		public bool WriteToFIX()
		{
			if (!isFIXSimulationStarted || _fixWriteMessage == null) return true;
		    var result = SendMessageInternal(_fixWriteMessage);
            if( result)
            {
                _fixWriteMessage = null;
            }
		    return result;
		}

        protected void ResendMessageProtected(FIXTMessage1_1 fixMessage)
        {
            if (isConnectionLost)
            {
                return;
            }
            var writePacket = fixSocket.MessageFactory.Create();
            var message = fixMessage.ToString();
            writePacket.DataOut.Write(message.ToCharArray());
            writePacket.SendUtcTime = TimeStamp.UtcNow.Internal;
            if (debug) log.DebugFormat(LogMessage.LOGMSG279, fixMessage);
            SendMessageInternal(writePacket);
        }

        private bool SendMessageInternal( Message message)
        {
            if (fixSocket.TrySendMessage(message))
            {
                IncreaseHeartbeat();
                if (trace) log.TraceFormat(LogMessage.LOGMSG144, message);
                return true;
            }
            log.Error("Failed to Write: " + message);
            Thread.Sleep(1000);
            Environment.Exit(1);
            return false;
        }

		private void IncreaseHeartbeat()
		{
		    var timeStamp = Factory.Parallel.UtcNow;
            timeStamp.AddMilliseconds(HeartbeatDelay);
            if (debug) log.DebugFormat(LogMessage.LOGMSG280, timeStamp, fixState);
            heartbeatTimer.Start(timeStamp);
		}		

        public void SendMessage(FIXTMessage1_1 fixMessage)
        {
            FixFactory.AddHistory(fixMessage);
            if (isConnectionLost)
            {
                RemoveTickSync(fixMessage);
                return;
            }
            var simulator = simulators[SimulatorType.SendDisconnect];
            if (simulator.CheckSequence(fixMessage.Sequence) )
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG274, fixMessage);
                ProviderSimulator.SwitchBrokerState("disconnect",false);
                isConnectionLost = true;
                return;
            }
            if (simulateSendFailed && IsRecovered && random.Next(50) == 4)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG281, fixMessage.Sequence, fixMessage);
                if( fixMessage.Type == "1")
                {
                    heartbeatResponseDeadline = TimeStamp.MaxValue;
                }
                if (debug) log.DebugFormat(LogMessage.LOGMSG282, fixMessage.Type);
                return;
            }
            var writePacket = fixSocket.MessageFactory.Create();
            var message = fixMessage.ToString();
            writePacket.DataOut.Write(message.ToCharArray());
            writePacket.SendUtcTime = TimeStamp.UtcNow.Internal;
            if( debug) log.DebugFormat(LogMessage.LOGMSG283, fixMessage);
            try
            {
                fixPacketQueue.Enqueue(writePacket, writePacket.SendUtcTime);
            }
            catch( QueueException ex)
            {
                if( ex.EntryType == EventType.Terminate)
                {
                    log.Warn("fix packet queue returned queue exception " + ex.EntryType + ". Dropping message due to dispose.");
                    Dispose();
                }
                else
                {
                    throw;
                }
            }

            simulator = simulators[SimulatorType.SendServerOffline];
            if (IsRecovered && FixFactory != null && simulator.CheckSequence( fixMessage.Sequence))
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG276, fixMessage);
                ProviderSimulator.SwitchBrokerState("offline",false);
                ProviderSimulator.SetOrderServerOffline();
                TrySendSessionStatus("3");
            }
        }

        protected void ReceivedHeartBeat()
        {
            if( fixState == ServerState.WaitingHeartbeat)
            {
                fixState = ServerState.Recovered;
            }
        }

        protected virtual Yield OnHeartbeat()
        {
			if( fixSocket != null && FixFactory != null)
			{
				var mbtMsg = (FIXTMessage1_1) FixFactory.Create();
				mbtMsg.AddHeader("1");
				if( trace) log.TraceFormat(LogMessage.LOGMSG166, mbtMsg);
                SendMessage(mbtMsg);
			    fixState = ServerState.WaitingHeartbeat;
                heartbeatResponseDeadline = Factory.Parallel.UtcNow;
                if (SyncTicks.Enabled)
                {
                    heartbeatResponseDeadline.AddMilliseconds(heartbeatResponseTimeout);
                }
                else
                {
                    heartbeatResponseDeadline.AddSeconds(heartbeatResponseTimeout);
                }

			}
			return Yield.DidWork.Return;
		}
		
		public void OnException(Exception ex)
		{
			log.Error("Exception occurred", ex);
		}

		protected volatile bool isDisposed = false;
        private int heartbeatResponseTimeout = 50;

        public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!isDisposed) {
				isDisposed = true;
				if (disposing) {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG48);
				    var sb = new StringBuilder();
				    var countFailedTests = 0;
                    foreach( var kvp in simulators)
                    {
                        var sim = kvp.Value;
                        if( sim.Enabled)
                        {
                            if( sim.Counter < sim.Minimum)
                            {
                                SyncTicks.Success = false;
                                ++countFailedTests;
                            }
                            sb.AppendLine(SyncTicks.CurrentTestName + ": " + sim.Type + " attempts " + sim.AttemptCounter + ", count " + sim.Counter);
                        }
                    }
                    if( countFailedTests > 0)
                    {
                        log.Error( countFailedTests + " negative simulators occured less than 2 times:\n" + sb);
                    }
                    else
                    {
                        log.Info("Active negative test simulator results:\n" + sb);
                    }
                    if( !ProviderSimulator.IsOrderServerOnline)
                    {
                        SyncTicks.Success = false;
                        log.Error("The FIX order server ended in offline state.");
                    }
                    else 
                    {
                        log.Info("The FIX order server finished up online.");
                    }
                    CloseSockets();
                    if (fixListener != null)
                    {
                        fixListener.Dispose();
                    }
                    if( task != null)
                    {
                        task.Stop();
                    }
                }
			}
		}

        public bool IsRecovered
        {
            get { return fixState == ServerState.Recovered || fixState == ServerState.WaitingHeartbeat;  }
        }

		public ushort FIXPort {
			get { return fixPort; }
		}

		public long RealTimeOffset {
			get { return realTimeOffset; }
		}

	    public int HeartbeatDelay
	    {
	        get { return heartbeatDelay; }
	    }

        public FIXTFactory1_1 FixFactory
        {
            get { return fixFactory; }
        }

        public int RemoteSequence
        {
            get { return remoteSequence; }
            set
            {
                if( remoteSequence != value)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG284, remoteSequence, value);
                    remoteSequence = value;
                }
            }
        }

        public ProviderSimulatorSupport ProviderSimulator
        {
            get { return providerSimulator; }
        }

        public abstract void OnRejectOrder(PhysicalOrder order, string error);
        public abstract void OnPhysicalFill(PhysicalFill fill, PhysicalOrder order);

        protected void OnBusinessReject(string error)
        {
            var mbtMsg = FixFactory.Create();
            mbtMsg.SetText(error);
            mbtMsg.AddHeader("j");
            if (trace) log.TraceFormat(LogMessage.LOGMSG285, mbtMsg);
            SendMessage(mbtMsg);
        }

        protected void OnBusinessRejectOrder(string clientOrderId, string error)
        {
            var mbtMsg = FixFactory.Create();
            mbtMsg.SetBusinessRejectReferenceId(clientOrderId);
            mbtMsg.SetText(error);
            mbtMsg.AddHeader("j");
            if (trace) log.TraceFormat(LogMessage.LOGMSG285, mbtMsg);
            SendMessage(mbtMsg);
        }

        protected void SendLogout()
        {
            var mbtMsg = FixFactory.Create();
            mbtMsg.AddHeader("5");
            SendMessage(mbtMsg);
            if (trace) log.TraceFormat(LogMessage.LOGMSG286, mbtMsg);
        }
	}
}