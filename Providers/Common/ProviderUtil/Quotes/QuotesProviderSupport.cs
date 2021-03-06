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
using System.Text;

using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
	public abstract class QuotesProviderSupport : AgentPerformer, LogAware
	{
		private readonly Log log;
		private volatile bool debug;
        private volatile bool trace;
        public virtual void RefreshLogLevel()
        {
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
        }
        protected class SymbolReceiver
        {
            public SymbolInfo Symbol;
            public Agent Agent;
        }
		protected readonly object symbolsRequestedLocker = new object();
        protected Dictionary<long, SymbolReceiver> symbolsRequested = new Dictionary<long, SymbolReceiver>();
		private Socket socket;
        protected Task socketTask;
		private string failedFile;
		protected Agent ClientAgent;
		private long retryDelay = 30; // seconds
		private long retryStart = 30; // seconds
		private long retryIncrease = 5;
		private long retryMaximum = 30;
		private volatile Status connectionStatus = Status.New;
		private string addrStr;
		private ushort port;
		private string userName;
		private	string password;
		public abstract void OnDisconnect();
		public abstract void OnRetry();
		public abstract void SendLogin();
        public abstract bool VerifyLogin();
        private string providerName;
		private long heartbeatTimeout;
		private int heartbeatDelay;
		private bool logRecovery = false;
	    private string configFilePath;
	    private string configSection;
        private bool useLocalTickTime = true;
        private volatile bool debugDisconnect = false;
	    private int timeSeconds = 10;
	    private TrueTimer taskTimer;
        private Agent agent;
	    private MessageFactory messageFactory;
        protected abstract void ProcessSocketMessage(Message rawMessage);
        protected abstract bool SendPing();

        public Agent Agent
        {
            get { return agent; }
            set { agent = value; }
        }

		public QuotesProviderSupport(string config, MessageFactory messageFactory)
		{
		    configSection = config;
		    this.messageFactory = messageFactory;
			log = Factory.SysLog.GetLogger(typeof(QuotesProviderSupport)+"."+GetType().Name);
		    log.Register(this);
            providerName = GetType().Name;
            RefreshLogLevel();
            string logRecoveryString = Factory.Settings["LogRecovery"];
            logRecovery = !string.IsNullOrEmpty(logRecoveryString) && logRecoveryString.ToLower().Equals("true");
            if( timeSeconds > 30)
            {
                log.Error("QuotesProvider retry time greater then 30 seconds: " + int.MaxValue);
            }
        }

        public void Initialize(Task task)
        {
            socketTask = task;
            socketTask.Scheduler = Scheduler.EarliestTime;
            taskTimer = Factory.Parallel.CreateTimer("Task", socketTask, TimerTask);
            if (debug) log.DebugFormat(LogMessage.LOGMSG184, taskTimer.StartTime);
            filter = socketTask.GetFilter();
            socketTask.Start();
            if (debug) log.DebugFormat(LogMessage.LOGMSG185);
            var appDataFolder = Factory.Settings["AppDataFolder"];
            if (appDataFolder == null)
            {
                throw new ApplicationException("Sorry, AppDataFolder must be set in the app.config file.");
            }
            var configFilePath = appDataFolder + @"/Providers/" + providerName + "/Default.config";
            failedFile = appDataFolder + @"/Providers/" + providerName + "/LoginFailed.txt";

            var configFile = LoadProperties(configFilePath);
            ParseProperties(configFile);

            if (File.Exists(failedFile))
            {
                throw new ApplicationException("Please correct the username or password error described in " + failedFile + ". Then delete the file retrying, please.");
            }
        }

        private void RegenerateSocket()
        {
			if( socket != null) {
				socket.Dispose();
			}
        }
		
		
		public enum Status {
			New,
			Connected,
			PendingLogin,
			PendingRecovery,
			Recovered,
			Disconnected,
			PendingRetry
		}
		
		public void FailLogin(string packetString) {
			string message = "Login failed for user name: " + userName + " and password: " + new string('*',password.Length);
			string fileMessage = "Resolve the problem and then delete this file before you retry.";
			string logMessage = "Resolve the problem and then delete the file " + failedFile + " before you retry.";
			if( File.Exists(failedFile)) {
				File.Delete(failedFile);
			}
			using( var fileOut = new StreamWriter(failedFile)) {
				fileOut.WriteLine(message);
				fileOut.WriteLine(fileMessage);
			}
			log.Error(message + " " + logMessage + "\n" + packetString);
			throw new ApplicationException(message + " " + logMessage);
		}

        private void OnConnect(Socket socket)
        {
            if (!this.socket.Equals(socket))
            {
                log.Warn("OnConnect( " + this.socket + " != " + socket + " ) - Ignored.");
                return;
            }
            log.Info("OnConnect( " + socket + " ) ");
            ConnectionStatus = Status.Connected;
            SendLogin();
            ConnectionStatus = Status.PendingLogin;
            IncreaseRetryTimeout();
        }

        protected void OnDisconnect(Socket socket)
        {
            if( !this.socket.Equals(socket))
            {
                log.Warn("OnDisconnect( " + this.socket + " != " + socket + " ) - Ignored.");
                return;
            }
			if( !this.socket.Port.Equals(socket.Port)) {
			}
			log.Info("OnDisconnect( " + socket + " ) ");
			ConnectionStatus = Status.Disconnected;
		    debugDisconnect = true;
            if (debug) log.DebugFormat(LogMessage.LOGMSG186, socket.State);
            if( isDisposed)
            {
                Finish();
            }
            else
            {
                log.Error("QuoteProvider disconnected.");
                CreateNewSocket();
            }
        }

        private void CreateNewSocket()
        {
            socket = Factory.Provider.Socket(providerName, addrStr, port);
            socket.OnConnect = OnConnect;
            socket.MessageFactory = messageFactory;
            socket.ReceiveQueue.ConnectInbound(socketTask);
            socket.SendQueue.ConnectOutbound(socketTask);
            if (debug) log.DebugFormat(LogMessage.LOGMSG187, socket);
            ConnectionStatus = Status.New;
            if (trace)
            {
                string message = "Generated socket: " + socket;
                log.TraceFormat(LogMessage.LOGMSG611, message);
            }
            // Initiate socket connection.
            while (true)
            {
                try
                {
                    socket.Connect();
                    log.Info("Requested Connect for " + socket);
                    retryTimeout = Factory.Parallel.TickCount + retryDelay * 1000;
                    log.Info("Connection will timeout and retry in " + retryDelay + " seconds.");
                    return;
                }
                catch (SocketErrorException ex)
                {
                    log.Error("Non fatal error while trying to connect: " + ex.Message);
                }
            }
        }

        protected void ReceivedPing()
        {
            isPingSent = false;
        }

        public bool IsInterrupted
        {
			get {
				return isDisposed || socket.State != SocketState.Connected;
			}
		}
	
		public void StartRecovery() {
			ConnectionStatus = Status.PendingRecovery;
			OnStartRecovery();
		}
		
		public void EndRecovery() {
			ConnectionStatus = Status.Recovered;
		}
		
		public bool IsRecovered {
			get { 
				return ConnectionStatus == Status.Recovered;
			}
		}
		
		private void SetupRetry() {
			OnRetry();
			RegenerateSocket();
		}
		
		public bool IsRecovering {
			get {
				return ConnectionStatus == Status.PendingRecovery;
			}
		}

	    private Status lastStatus;
        private SocketState lastSocketState;

        private Yield TimerTask()
        {
            if (isDisposed) return Yield.NoWork.Repeat;
            TimeStamp currentTime = TimeStamp.UtcNow;
            currentTime.AddSeconds(timeSeconds);
            taskTimer.Start(currentTime);
            if (debug) log.DebugFormat(LogMessage.LOGMSG184, taskTimer.StartTime);
            return Invoke();
        }

        public void Shutdown()
        {
            Dispose();
        }

        public Yield Invoke()
        {
			if( isDisposed ) return Yield.NoWork.Repeat;
            EventItem eventItem;
            if( filter.Receive(out eventItem))
            {
                switch( eventItem.EventType)
                {
                    case EventType.Connect:
                        Start(eventItem);
                        filter.Pop();
                        break;
                    case EventType.Disconnect:
                        Stop(eventItem);
                        filter.Pop();
                        break;
                    case EventType.StartSymbol:
                        StartSymbol(eventItem);
                        filter.Pop();
                        break;
                    case EventType.StopSymbol:
                        StopSymbol(eventItem);
                        filter.Pop();
                        break;
                    case EventType.PositionChange:
                        PositionChange(eventItem);
                        filter.Pop();
                        break;
                    case EventType.SyntheticOrder:
                        SyntheticOrder(eventItem);
                        filter.Pop();
                        break;
                    case EventType.SyntheticClear:
                        SyntheticClear(eventItem.Symbol);
                        filter.Pop();
                        break;
                    case EventType.Shutdown:
                    case EventType.Terminate:
                        Dispose();
                        filter.Pop();
                        break;
                    default:
                        throw new ApplicationException("Unexpected event: " + eventItem);
                }
            }
            if (!isStarted) return Yield.NoWork.Repeat;

            if (debugDisconnect)
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG188, socket.State, socket);
                debugDisconnect = false;
            }
            if( socket.State != lastSocketState)
            {
                if( debug) log.DebugFormat(LogMessage.LOGMSG189, socket.State);
                lastSocketState = socket.State;
            }
            if (ConnectionStatus != lastStatus)
            {
                lastStatus = ConnectionStatus;
            }
            switch (socket.State)
            {
				case SocketState.New:
    				return Yield.NoWork.Repeat;
				case SocketState.PendingConnect:
					if( Factory.Parallel.TickCount >= retryTimeout) {
                        log.Info(providerName + " connect timed out. Retrying.");
						SetupRetry();
						retryDelay += retryIncrease;
                        IncreaseRetryTimeout();
						return Yield.DidWork.Repeat;
					} else {
						return Yield.NoWork.Repeat;
					}
				case SocketState.Connected:
					return TryProcessMessage();
                case SocketState.ShuttingDown:
                case SocketState.Closing:
                case SocketState.Closed:
                case SocketState.Disconnected:
					return TrySetupRetry();
                default:
					string errorMessage = "Unknown socket state: " + socket.State;
                    log.Error(errorMessage);
                    throw new ApplicationException(errorMessage);
			}
		}

	    private void SyntheticClear(SymbolInfo symbol)
	    {
            SymbolHandler symbolHandler;
            lock (symbolHandlersLocker)
            {
                if (symbolHandlers.TryGetValue(symbol.BinaryIdentifier, out symbolHandler))
                {
                    symbolHandler.SyntheticClear();
                }
            }
        }

	    private void SyntheticOrder(EventItem eventItem)
	    {
	        var order = (PhysicalOrder) eventItem.EventDetail;
            SymbolHandler symbolHandler;
            lock (symbolHandlersLocker)
            {
                if (!symbolHandlers.TryGetValue(order.Symbol.BinaryIdentifier, out symbolHandler))
                {
                    log.Info("SymbolHandler for " + order.Symbol + " was not found.");
                    return;
                }
            }
	        symbolHandler.SyntheticOrder(eventItem);
	    }

	    private Yield TrySetupRetry()
	    {
	        switch( ConnectionStatus) {
                case Status.PendingRecovery:
                case Status.Recovered:
                    return TryProcessMessage();
                case Status.New:
                case Status.Disconnected:
	                retryTimeout = Factory.Parallel.TickCount + retryDelay * 1000;
	                ConnectionStatus = Status.PendingRetry;
	                if( debug) log.DebugFormat(LogMessage.LOGMSG190, retryDelay);
	                retryDelay += retryIncrease;
	                retryDelay = retryDelay > retryMaximum ? retryMaximum : retryDelay;
	                return Yield.NoWork.Repeat;
	            case Status.PendingRetry:
	                if( Factory.Parallel.TickCount >= retryTimeout) {
	                    log.Info(providerName + " retry time elapsed. Retrying.");
	                    OnRetry();
	                    CreateNewSocket();
	                    return Yield.DidWork.Repeat;
	                } else {
	                    return Yield.NoWork.Repeat;
	                }
	            default:
	                log.Warn("Unexpected state for quotes connection: " + ConnectionStatus);
	                ConnectionStatus = Status.Disconnected;
	                log.Warn("Forces connection state to be: " + ConnectionStatus);
	                return Yield.NoWork.Repeat;
	        }
	    }

        private Yield TryProcessMessage()
	    {
	        switch( ConnectionStatus) {
                case Status.New:
                case Status.Connected:
	                return Yield.NoWork.Repeat;
	            case Status.PendingLogin:
	                if( VerifyLogin())
	                {
	                    StartRecovery();
	                    return Yield.DidWork.Repeat;
	                }
	                else
	                {
	                    return Yield.NoWork.Repeat;
	                }
	            case Status.PendingRecovery:
	            case Status.Recovered:
	                if( retryDelay != retryStart) {
	                    retryDelay = retryStart;
	                    log.Info("(RetryDelay reset to " + retryDelay + " seconds.)");
	                }
	                if( Factory.Parallel.TickCount >= heartbeatTimeout) {
	                    if( !isPingSent)
	                    {
	                        isPingSent = SendPing();
	                        IncreaseRetryTimeout();
	                    }
	                    else
	                    {
	                        if( SyncTicks.Frozen)
	                        {
	                            log.Error("SyncTicks is frozen so skipping retry on quotes heartbeat timeout.");
	                            heartbeatTimeout = long.MaxValue;
	                        }
	                        else
	                        {
	                            isPingSent = false;
	                            log.Warn("QuotesProvider ping timed out.");
	                            SetupRetry();
	                            IncreaseRetryTimeout();
	                            return Yield.DidWork.Repeat;
	                        }
	                    }
	                }
	                Message rawMessage;
	                var receivedMessage = false;
	                if (Socket.TryGetMessage(out rawMessage))
	                {
	                    var disconnect = rawMessage as DisconnectMessage;
	                    if( disconnect != null)
	                    {
	                        OnDisconnect(disconnect.Socket);
	                    }
	                    else
	                    {
	                        receivedMessage = true;
	                        ProcessSocketMessage(rawMessage);
	                        Socket.MessageFactory.Release(rawMessage);
	                    }
	                }
	                if( receivedMessage) {
	                    IncreaseRetryTimeout();
	                }

	                return receivedMessage ? Yield.DidWork.Repeat : Yield.NoWork.Repeat;
	            default:
	                throw new ApplicationException("Unexpected connection status: " + ConnectionStatus);
	        }
	    }

	    private bool isPingSent = false;
		protected void IncreaseRetryTimeout() {
			retryTimeout = Factory.Parallel.TickCount + retryDelay * 1000;
			heartbeatTimeout = Factory.Parallel.TickCount + (long)heartbeatDelay * 1000L;
		}
		
		protected abstract void OnStartRecovery();
		
		private long retryTimeout;
		
		private void OnException( Exception ex) {
			// Attempt to propagate the exception.
			log.Error("Exception occurred", ex);
			SendError( ex.Message + "\n" + ex.StackTrace);
			Dispose();
		}

	    private QueueFilter filter;
	    private bool isStarted;
        public void Start(EventItem eventItem)
        {
        	this.ClientAgent = eventItem.Agent;
            log.Info(providerName + " Startup");

            TimeStamp currentTime = TimeStamp.UtcNow;
            currentTime.AddSeconds(timeSeconds);
            taskTimer.Start(currentTime);
            if (debug) log.DebugFormat(LogMessage.LOGMSG184, taskTimer.StartTime);

            CreateNewSocket();
            isStarted = true;
        }
        
        public void Stop(EventItem eventItem) {
        	
        }
	
        public void StartSymbol(EventItem eventItem)
        {
        	log.Info("StartSymbol( " + eventItem.Symbol+ ")");
            TryAddSymbol(eventItem.Symbol, eventItem.Agent);
            OnStartSymbol(eventItem.Symbol, eventItem.Agent);
        }
        
        public abstract void OnStartSymbol( SymbolInfo symbol, Agent symbolAgent);
        
        public void StopSymbol(EventItem eventItem)
        {
        	log.Info("StopSymbol( " + eventItem.Symbol + ")");
            if (TryRemoveSymbol(eventItem.Symbol))
            {
                OnStopSymbol(eventItem.Symbol, eventItem.Agent);
        	}
        }
        
        public abstract void OnStopSymbol(SymbolInfo symbol, Agent symbolAgent);

	    private bool alreadyLoggedSectionAndFile = false;
	    protected virtual ConfigFile LoadProperties(string configFilePath) {
	        this.configFilePath = configFilePath;
            if( !alreadyLoggedSectionAndFile)
            {
                log.Notice("Using section " + configSection + " in file: " + configFilePath);
                alreadyLoggedSectionAndFile = true;
            }
	        return new ConfigFile(configFilePath);
		}
	        
        protected virtual void ParseProperties(ConfigFile configFile) {
			var value = GetField("UseLocalTickTime",configFile, false);
			if( !string.IsNullOrEmpty(value)) {
				useLocalTickTime = value.ToLower() != "false";
        	}
			
			AddrStr = GetField("ServerAddress",configFile,true);
			var portStr = GetField("ServerPort",configFile,true);
			if( !ushort.TryParse(portStr, out port)) {
				Exception("ServerPort",configFile);
			}
			userName = GetField("UserName",configFile,true);
			password = GetField("Password",configFile,true);
			
			if( File.Exists(failedFile) ) {
				throw new ApplicationException("Please correct the username or password error described in " + failedFile + ". Then delete the file before retrying, please.");
			}
        }

	    protected string GetField( string field, ConfigFile configFile, bool required) {
			var result = configFile.GetValue(configSection + "/" + field);
			if( required && string.IsNullOrEmpty(result)) {
				Exception( field, configFile);
			}
			return result;
        }
        
        private void Exception( string field, ConfigFile configFile) {
        	var sb = new StringBuilder();
        	sb.AppendLine("Sorry, an error occurred finding the '" + field +"' setting.");
        	sb.AppendLine("Please either set '" + field +"' in section '"+configSection+"' of '"+configFile+"'.");
            sb.AppendLine("Otherwise you may choose a different section within the config file.");
            sb.AppendLine("You can choose the section either in your project.tzproj file or");
            sb.AppendLine("if you run a standalone ProviderService, in the ProviderServer\\Default.config file.");
            sb.AppendLine("In either case, you may set the ProviderAssembly value as <AssemblyName>/<Section>");
            sb.AppendLine("For example, " + providerName + "/EquityDemo will choose the " + providerName + ".exe assembly");
            sb.AppendLine("with the EquityDemo section within the " + providerName + "\\Default.config file for that assembly.");
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
		
		public bool GetSymbolStatus(SymbolInfo symbol) {
			lock( symbolsRequestedLocker) {
				return symbolsRequested.ContainsKey(symbol.BinaryIdentifier);
			}
		}
		
		private bool TryAddSymbol(SymbolInfo symbol, Agent symbolAgent) {
			lock( symbolsRequestedLocker) {
				if( !symbolsRequested.ContainsKey(symbol.BinaryIdentifier))
				{
				    symbolsRequested.Add(symbol.BinaryIdentifier, new SymbolReceiver {Symbol = symbol, Agent = symbolAgent});
					return true;
				}
			}
			return false;
		}
		
		private bool TryRemoveSymbol(SymbolInfo symbol) {
			lock( symbolsRequestedLocker) {
				if( symbolsRequested.ContainsKey(symbol.BinaryIdentifier)) {
					symbolsRequested.Remove(symbol.BinaryIdentifier);
					return true;
				}
			}
			return false;
		}
		
		public void PositionChange(EventItem eventItem)
		{
		    
		}

        private volatile bool isFinished;
        public bool IsFinalized
        {
            get { return isFinished && (socketTask == null || !socketTask.IsAlive); }
        }

        public void Finish()
        {
            isFinished = true;
            if (socketTask != null)
            {
                socketTask.Stop();
                if (debug) log.DebugFormat(LogMessage.LOGMSG191);
            }
        }

        protected volatile bool isDisposed = false;
	    protected object symbolHandlersLocker = new object();
	    protected Dictionary<long, SymbolHandler> symbolHandlers = new Dictionary<long, SymbolHandler>();
	    protected Dictionary<long, SymbolHandler> symbolOptionHandlers = new Dictionary<long, SymbolHandler>();

	    public void Dispose() 
	    {
	        Dispose(true);
	        GC.SuppressFinalize(this);      
	    }
	
	    protected virtual void Dispose(bool disposing)
	    {
       		if( !isDisposed) {
                if (debug) log.DebugFormat(LogMessage.LOGMSG48);
	            isDisposed = true;   
	            if (disposing) {
                    if (socket != null)
                    {
                        socket.Dispose();
                    }
                    if (taskTimer != null)
                    {
                        taskTimer.Dispose();
                        if (debug) log.DebugFormat(LogMessage.LOGMSG192);
                    }
                    if( socketTask != null)
                    {
                        socketTask.Stop();
                    }
	            }
    		}
	    }
		
		public Socket Socket {
			get { return socket; }
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
		
		public long RetryStart {
			get { return retryStart; }
			set { retryStart = retryDelay = value; }
		}
		
		public long RetryIncrease {
			get { return retryIncrease; }
			set { retryIncrease = value; }
		}
		
		public long RetryMaximum {
			get { return retryMaximum; }
			set { retryMaximum = value; }
		}
		
		public int HeartbeatDelay {
			get { return heartbeatDelay; }
			set { heartbeatDelay = value;
                if( heartbeatDelay > 10)
                {
                    log.Error("Heartbeat delay is " + heartbeatDelay);
                }
				IncreaseRetryTimeout();
			}
		}
		
		public bool LogRecovery {
			get { return logRecovery; }
		}
		
		public Status ConnectionStatus
		{
		    get { return connectionStatus; }
		    set
		    {
		        if( connectionStatus != value)
		        {
		            if( debug) log.DebugFormat(LogMessage.LOGMSG193, connectionStatus, value);
		            connectionStatus = value;
		        }
		    }
		}

	    public bool UseLocalTickTime {
			get { return useLocalTickTime; }
		}

	    protected SymbolHandler StartSymbolHandler(SymbolInfo symbol, Agent agent) {
	        lock( symbolHandlersLocker) {
	            SymbolHandler symbolHandler;
                if (symbolHandlers.TryGetValue(symbol.CommonSymbol.BinaryIdentifier, out symbolHandler))
                {
	                symbolHandler.Start();
                    log.InfoFormat("Found symbol handler for {0}, id {1}, common {1}, id {2}", symbol, symbol.BinaryIdentifier, symbol.CommonSymbol, symbol.CommonSymbol.BinaryIdentifier);
                }
                else
                {
	                symbolHandler = Factory.Utility.SymbolHandler(providerName, symbol,agent);
	                symbolHandlers.Add(symbol.CommonSymbol.BinaryIdentifier,symbolHandler);
	                symbolHandler.Start();
                    log.InfoFormat("Added symbol handler for {0}, id {1}, common {1}, id {2}", symbol, symbol.BinaryIdentifier, symbol.CommonSymbol, symbol.CommonSymbol.BinaryIdentifier);
	            }
	            return symbolHandler;
	        }
	    }

	    protected void StartSymbolOptionHandler(SymbolInfo symbol, Agent agent)
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
	                symbolHandler = Factory.Utility.SymbolHandler(providerName, symbol, agent);
	                symbolOptionHandlers.Add(symbol.BinaryIdentifier, symbolHandler);
	                symbolHandler.Start();
	            }
	        }
	    }
	}
}
