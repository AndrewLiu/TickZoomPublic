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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

using log4net.Core;
using TickZoom.Api;

namespace TickZoom.Logging
{
    /// <summary>
	/// Description of TickConsole.
	/// </summary>
    [Serializable]
    public class LogImpl : Log
    {
		private readonly static Type callingType = typeof(LogImpl);
		private LogImplWrapper logWrapper;
		private static Dictionary<string,string> symbolMap;

	    #region OldStuff
        private static LoggingQueue messageQueue = null; 
        private static object locker = new object();
        private string fileName;
	    private LogManagerImpl logManager;
        private bool allowDebugging = false;
        private static Dictionary<string,int> uniqueLoggers = new Dictionary<string,int>();
		
        public LogImpl(LogManagerImpl manager, ILogger logger)
        {
            switch( logger.Name)
            {
                case "TestLog.TradeLog":
                case "TestLog.TransactionLog":
                case "TestLog.StatsLog":
                case "TestLog.BarDataLog":
                    allowDebugging = true;
                    break;
            }

            logManager = manager;
			logWrapper = new LogImplWrapper(logger);
            uniqueLoggers[logWrapper.Logger.Name] = 1;
        	Connect();
			if( symbolMap == null) {
				lock( locker) {
        			if( symbolMap == null) {
        				ConvertSymbols();
        			}
        		}
			}
       	}

        internal void NofityLogLevelChange()
        {
            logWrapper.ReloadLevels();
        }
        
 		private void ConvertSymbols() {
			symbolMap = new Dictionary<string, string>();
			string symbols = Factory.Settings["LogSymbols"];
			if( symbols != null) {
				string[] array = symbols.Split(',');
				for( int i=0;i<array.Length; i++) {
					string symbol = array[i].Trim();
					if( symbol.Length>0) {
						symbolMap[symbol] = null;
					}
				}
			}
		}
		
		private void Connect() {
        	if( messageQueue == null) {
        		lock( locker) {
		        	if( messageQueue == null) {
        				messageQueue = new LoggingQueue();
	       			}
        		}
        	}
        }

        public Log LogFile(string fileName) {
        	throw new NotImplementedException();
	    }
        
        public void WriteLine(string msg)
        {
        	Info(msg);
        }
        
        private void WriteScreen(LoggingEvent msg) {
        	if( messageQueue != null) {
                messageQueue.EnQueue(msg);
    	    }
        }
        
        public void Clear() {
        }
        
        public void WriteFile(string msg)
        {
      		DebugFormat(msg);
        }
        
        public bool HasLine {
        	get {
        		return messageQueue.Count > 0;
        	}
        }
        public bool TryReadLine(out LogEvent[] logEvents) {
        	if( messageQueue == null) {
        		throw new ApplicationException( "Sorry. You must Connect before ReadLine");
        	}
        	LoggingEvent msg = default(LoggingEvent);
            if( messageQueue.Count > 0)
            {
                logEvents = new LogEvent[messageQueue.Count];
                for( var i=0; i< logEvents.Length; i++)
                {
                    if (messageQueue.Dequeue(out msg))
                    {
                        logEvents[i] = new LogEventDefault()
                                          {
                                              IsAudioAlarm = msg.Level >= Level.Error,
                                              Color = msg.Level >= Level.Error ? Color.Red : msg.Level >= Level.Warn ? Color.Yellow : Color.Empty,
                                              Message = msg.Level + ": " + msg.RenderedMessage,
                                          };
                    }
                    else
                    {
                        break;
                    }
                }

                return true;
            }
            logEvents = null;
            return false;
        }
        
        int indent = 0;
        
        public void Indent() {
        	indent += 2;
        	AdjustIndentString();
        }
        
        public void Outdent() {
        	indent -= 2;
        	AdjustIndentString();
        }

        private void AdjustIndentString() {
       		if( indent < 0) {
        		indent = 0;
        	}
        }
        
 		public string FileName {
			get { return fileName; }
			set { fileName = value; }
		}
        
#endregion
		public TimeStamp TimeStamp {
			get { return new TimeStamp(log4net.MDC.Get("TimeStamp")); }
			set { log4net.MDC.Set("TimeStamp",value.ToString()); }
		}

		public string Symbol {
			get { return log4net.MDC.Get("Symbol"); }
			set { log4net.MDC.Set("Symbol",value); }
		}

		public int CurrentBar {
			get { int bar;
				if( int.TryParse(log4net.MDC.Get("CurrentBar"),out bar)) {
					return bar;
				}else {
					throw new FormatException("Unable to parse " + log4net.MDC.Get("CurrentBar") + " as in int.");
				}
			}
			set { log4net.MDC.Set("CurrentBar",value.ToString()); }
		}
		
		public bool IsNoticeEnabled {
			get { return LogWrapper.IsNoticeEnabled; }
		}
		
		public bool IsVerboseEnabled {
			get { return LogWrapper.IsVerboseEnabled; }
		}
		
		public bool IsTraceEnabled {
			get { return LogWrapper.IsTraceEnabled; }
		}
		
		public bool IsDebugEnabled {
			get { return LogWrapper.IsDebugEnabled; }
		}
		
		public bool IsInfoEnabled {
			get { return LogWrapper.IsInfoEnabled; }
		}
		
		public bool IsWarnEnabled {
			get { return LogWrapper.IsWarnEnabled; }
		}
		
		public bool IsErrorEnabled {
			get { return LogWrapper.IsErrorEnabled; }
		}
		
		public bool IsFatalEnabled {
			get { return LogWrapper.IsFatalEnabled; }
		}

	    internal LogImplWrapper LogWrapper
	    {
	        get { return logWrapper; }
	    }

        public List<ArgumentHandler> UniqueTypes
        {
            get
            {
                var list = new List<ArgumentHandler>();
                foreach( var storage in logThread)
                {
                    if (storage == null) continue;
                    foreach( var kvp in storage.UniqueTypesInternal)
                    {
                        var handler = kvp.Value;
                        list.Add(handler);
                    }
                }
                return list;
            }
        }

        public List<FormatHandler> UniqueFormats
        {
            get
            {
                var list = new List<FormatHandler>();
                foreach (var storage in logThread)
                {
                    if (storage == null) continue;
                    foreach (var kvp in storage.UniqueFormatsInternal)
                    {
                        var handler = kvp.Value;
                        list.Add(handler);
                    }
                }
                return list;
            }
        }

        public void Assert(bool test) {
			if( test == false) {
				Exception ex = new AssertFailedException(new StackTrace(1,true));
				LogWrapper.Error("Assertion Failed", ex);
				throw ex;
			}
		}
		
		public void Notice(object message)
      	{
			Notice(message,null);
		}
		
		public void Info(object message)
		{
			Info(message,null);
        }
        
		public void Warn(object message)
		{
			Warn(message,null);
		}
        
		public void Error(object message)
		{
			Error(message,null);
		}
		
		public void Fatal(object message)
		{
			Fatal(message,null);
		}
		
		public void Notice(object message, Exception t)
		{
			if (IsNoticeEnabled)
			{
				var data = BuildEventData(LogWrapper.Logger.Name, Level.Notice, message, t);
				var loggingEvent = new LoggingEvent( callingType, LogWrapper.Logger.Repository, data);
				if( t!=null) {
					System.Diagnostics.Debug.WriteLine(message + "\n" + t);
				}
			    WriteScreen(loggingEvent);
				LogWrapper.Logger.Log(loggingEvent);
			}
		}
		
		public void Verbose(object message, Exception t)
		{
			if (IsVerboseEnabled)
			{
				var data = BuildEventData(LogWrapper.Logger.Name, Level.Verbose, message, t);
				var loggingEvent = new LoggingEvent( callingType, LogWrapper.Logger.Repository, data);
				if( t!=null) {
					System.Diagnostics.Debug.WriteLine(message + "\n" + t);
				}
			    LogWrapper.Logger.Log(loggingEvent);
			}
		}
		
		public void Trace(object message, Exception t)
		{
			if (IsTraceEnabled)
			{
				var data = BuildEventData(LogWrapper.Logger.Name, Level.Trace, message, t);
				var loggingEvent = new LoggingEvent( callingType, LogWrapper.Logger.Repository, data);
				if( t!=null) {
					System.Diagnostics.Debug.WriteLine(message + "\n" + t);
				}
			    LogWrapper.Logger.Log(loggingEvent);
			}
		}
		
		public void Call() {
			if (IsDebugEnabled)
			{
				StackTrace trace = new StackTrace();
				MethodBase callee = trace.GetFrame(1).GetMethod();
				Type calleeObj = callee.DeclaringType;
				MethodBase caller = trace.GetFrame(2).GetMethod();
				Type callerObj = caller.DeclaringType;
				if( callee.Name == ".ctor") {
					DebugFormat(GetTypeName(callerObj) + " (!) " + GetTypeName(calleeObj) );
				} else {
					DebugFormat(GetTypeName(callerObj) + " ==> " + GetTypeName(calleeObj) + " " + GetSignature(callee));
				}
			}
		}
		
		public void Async(Action action) {
			if (IsDebugEnabled)
			{
				StackTrace trace = new StackTrace();
				MethodBase caller = trace.GetFrame(1).GetMethod();
				Type callerObj = caller.DeclaringType;
				Delegate[] del = action.GetInvocationList();
				MethodInfo callee = del[0].Method;
				Type calleeObj = callee.DeclaringType;
				DebugFormat(GetTypeName(callerObj) + " >-- " + GetTypeName(calleeObj) + " " + GetSignature(callee));
				DebugFormat(GetTypeName(callerObj) + " --> " + GetTypeName(calleeObj) + " " + GetSignature(callee));
			}
		}
		
		public void Return() {
			if (IsDebugEnabled)
			{
				StackTrace trace = new StackTrace();
				MethodBase callee = trace.GetFrame(1).GetMethod();
				Type calleeObj = callee.DeclaringType;
				MethodBase caller = trace.GetFrame(2).GetMethod();
				Type callerObj = caller.DeclaringType;
				DebugFormat(GetTypeName(callerObj) + " <== " + GetTypeName(calleeObj) + " " + GetSignature(callee));
			}
		}
		
		private string StripTilda(string typeName) {
			return typeName.Substring(0,typeName.IndexOf('`'));
		}
		
		private string GetSignature(MethodBase method) {
			ParameterInfo[] parameters = method.GetParameters();
			StringBuilder builder = new StringBuilder();
			builder.Append(method.Name);
			builder.Append("(");
			for( int i=0; i<parameters.Length; i++) {
				if( i!=0) builder.Append(",");
				ParameterInfo parameter = parameters[i];
				Type type = parameter.ParameterType;
				builder.Append(GetTypeName(type));
			}
			builder.Append(")");
			return builder.ToString();
		}

		private string GetTypeName(Type type) {
			Type[] generics = type.GetGenericArguments();
			if( generics.Length>0) {
				StringBuilder builder = new StringBuilder();
				builder.Append(StripTilda(type.Name));
				builder.Append("<");
				for( int j=0; j<generics.Length; j++) {
					if( j!=0) builder.Append(",");
					Type generic = generics[j];
					builder.Append(generic.Name);
				}
				builder.Append(">");
				return builder.ToString();
			} else {
				return type.Name;
			}
		}
		public void Debug(string message, Exception t)
		{
			if (IsDebugEnabled)
			{
				var data = BuildEventData(LogWrapper.Logger.Name, Level.Debug, message, t);
				var loggingEvent = new LoggingEvent( callingType, LogWrapper.Logger.Repository, data);
				if( t!=null) {
					System.Diagnostics.Debug.WriteLine(message + "\n" + t);
				}
			    LogWrapper.Logger.Log(loggingEvent);
			}
		}
		
		public void Info(object message, Exception t)
		{
			if (IsInfoEnabled)
			{
				var data = BuildEventData(LogWrapper.Logger.Name, Level.Info, message, t);
				var loggingEvent = new LoggingEvent( callingType, LogWrapper.Logger.Repository, data);
				if( t!=null) {
					System.Diagnostics.Debug.WriteLine(message + "\n" + t);
				}
			    LogWrapper.Logger.Log(loggingEvent);
			}
		}
		
		private LoggingEventData BuildEventData(string logger, Level level, object message, Exception exception)
		{
			string exceptionString = null;
			if( exception != null) {
				exceptionString = exception.ToString();
			}
			var data = new LoggingEventData() {
				LoggerName = logger,
				Level = level, 
				Message = message.ToString(),
				ThreadName = Thread.CurrentThread.Name,
                TimeStamp = Factory.ParallelInitialized ? Factory.Parallel.UtcNow.DateTime : TimeStamp.UtcNow.DateTime,
				ExceptionString = exceptionString,
			};
			return data;
		}
		
		public void Warn(object message, Exception t)
		{
			if (IsWarnEnabled)
			{
				var data = BuildEventData(LogWrapper.Logger.Name, Level.Warn, message, t);
				var loggingEvent = new LoggingEvent( callingType, LogWrapper.Logger.Repository, data);
				if( t!=null) {
					System.Diagnostics.Debug.WriteLine(message + "\n" + t);
				}
			    WriteScreen(loggingEvent);
				LogWrapper.Logger.Log(loggingEvent);
                logManager.Flush();
			}
		}
		
		public void Error(object message, Exception t)
		{
			if (IsErrorEnabled)
			{
				var data = BuildEventData(LogWrapper.Logger.Name, Level.Error, message, t);
				var loggingEvent = new LoggingEvent( callingType, LogWrapper.Logger.Repository, data);
				if( t!=null) {
					System.Diagnostics.Debug.WriteLine(message + "\n" + t);
				}
			    WriteScreen(loggingEvent);
				LogWrapper.Logger.Log(loggingEvent);
                logManager.Flush();
            }
		}

        public void Fatal(object message, Exception t)
		{
			if (IsFatalEnabled)
			{
				LoggingEvent loggingEvent = new LoggingEvent(callingType, LogWrapper.Logger.Repository, LogWrapper.Logger.Name, Level.Fatal, message, t);
			    WriteScreen(loggingEvent);
				LogWrapper.Logger.Log(loggingEvent);
			}
		}

        private void SerializeArgument<T>(T arg)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var storage = logThread[threadId];
            if( storage == null)
            {
                storage = logThread[threadId] = new LogThreadStorage();
            }
            if( arg == null)
            {
                return;
            }
            var type = arg.GetType();
            ArgumentHandler argumentHandler;
            if (storage.UniqueTypesInternal.TryGetValue(type, out argumentHandler))
            {
                argumentHandler.Count++;
            }
            else
            {
                argumentHandler = CreateArgumentHandler(type);
                storage.UniqueTypesInternal.Add(type, argumentHandler);
            }
            switch (argumentHandler.ArgumentType)
            {
                case ArgumentType.Actual:
                    if (!type.IsValueType && type != typeof(string))
                    {
                        storage.EncoderDecoder.Encode(storage.MemoryBuffer, arg);
                    }
                    break;
                case ArgumentType.ToString:
                    //encoderDecoder.Encode(memoryBuffer, arg.ToString());
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unknown argument type: " + argumentHandler.ArgumentType);
            }
        }

        private ArgumentHandler CreateArgumentHandler(Type type)
        {
            var argumentHandler = new ArgumentHandler();
            argumentHandler.Type = type;
            switch (type.FullName)
            {
                case "TickZoom.Api.TickSync": // 1994
                    throw new ApplicationException("Use State property to log TickSync.");
                case "TickZoom.TickUtil.TickImpl": // 3
                    throw new ApplicationException("Use Extract() method to log TickImpl.");
                case "TickZoom.Api.TickBox": // 533302
                    throw new ApplicationException("Use Tick property to log TickBox.");
                case "TickZoom.Api.TickBinaryBox": // 89
                    throw new ApplicationException("Use TickBinary property to log TickBinaryBox.");
                case "TickZoom.Api.LogicalOrderDefault":
                case "TickZoom.Api.PhysicalOrderDefault": // 36877
                case "TickZoom.Api.LogicalFillDefault": // 15047
                case "TickZoom.Api.PhysicalFillDefault": // 11285
                case "TickZoom.Api.LogicalFillBinaryBox": // 3761
                case "TickZoom.Api.TransactionPairBinary": // 3761
                case "TickZoom.Api.TimeStamp": // 3317
                case "TickZoom.Api.IntervalImpl": // 45
                case "TickZoom.Api.PositionChangeDetail": // 5498
                    argumentHandler.ArgumentType = ArgumentType.Actual;
                    break;
                case "TickZoom.Engine.StrategyPositionWrapper": // 397
                case "TickZoom.Symbols.SymbolProperties": // 34259
                case "TickZoom.Internals.ModelDriver": // 455
                case "TickZoom.PriceData.TimeFrameSeries": // 45
                case "TickZoom.PriceData.PriceSeries": // 15
                case "TickZoom.Threading.AgentProxy": // 48
                case "TickZoom.SocketAPI.SocketTCP": // 123
                case "TickZoom.SocketAPI.SocketSharedMemory": // 123
                case "TickZoom.Provider.FIX.FIXMessage4_2": // 1502
                case "TickZoom.Provider.FIX.MessageFIX4_2": // 3151
                case "TickZoom.Provider.FIX.FIXMessage4_4": // 1502
                case "TickZoom.Provider.FIX.MessageFIX4_4": // 3151
                case "System.RuntimeType": // 3151
                    argumentHandler.ArgumentType = ArgumentType.ToString;
                    break;
                default:
                    if (type.IsValueType || type == typeof (string))
                    {
                        argumentHandler.ArgumentType = ArgumentType.Actual;
                    }
                    else if (type.IsSubclassOf(typeof (Delegate)))
                    {
                        throw new ApplicationException("Use GetType().FullName property to log a delegate.");
                    }
                    else if (type.GetInterface(typeof (StrategyInterface).FullName) != null)
                    {
                        argumentHandler.ArgumentType = ArgumentType.ToString;
                    }
                    else
                    {
                        argumentHandler.ArgumentType = ArgumentType.ToString;
                        argumentHandler.IsUnknownType = true;
                    }
                    break;
            }
            return argumentHandler;
        }

        public string resultString;
        public string cloneResult;

        private bool genericSerialized = false;

        public class LogThreadStorage
        {
            public EncodeHelper EncoderDecoder = new EncodeHelper();
            public MemoryStream MemoryBuffer = new MemoryStream();
            public Dictionary<string, FormatHandler> UniqueFormatsInternal = new Dictionary<string, FormatHandler>();
            public Dictionary<Type, ArgumentHandler> UniqueTypesInternal = new Dictionary<Type, ArgumentHandler>();
        }

        private static LogThreadStorage[] logThread = new LogThreadStorage[1000];

		private void VerboseInternal(string format, params object[] args)
		{
            if( IsVerboseEnabled)
            {
                resultString = string.Format(format, args);
                Verbose(resultString, null);
            }
		}

        private void TraceInternal(string format, params object[] args)
		{
			if( IsTraceEnabled)
			{
                resultString = string.Format(format, args);
                Trace(resultString, null);
            }
		}
		
		private void DebugInternal(string format, params object[] args)
		{
            if( IsDebugEnabled)
            {
                resultString = string.Format(format, args);
                Debug(resultString, null);
            }
		}
		
		private void VerboseInternal(LogMessage formatKey, params object[] args)
		{
            if( IsVerboseEnabled)
            {
                resultString = string.Format(Factory.SysLog.Formats[formatKey], args);
                Verbose(resultString, null);
            }
		}

        private void TraceInternal(LogMessage formatKey, params object[] args)
        {
            if (IsTraceEnabled)
            {
                resultString = string.Format(Factory.SysLog.Formats[formatKey], args);
                Trace(resultString, null);
            }
        }

        private void DebugInternal(LogMessage formatKey, params object[] args)
        {
            if (IsDebugEnabled)
            {
                resultString = string.Format(Factory.SysLog.Formats[formatKey], args);
                Debug(resultString, null);
            }
        }
		
		public void InfoFormat(string format, params object[] args)
		{
			LogWrapper.InfoFormat(format, args);
		}
		
		public void NoticeFormat(string format, params object[] args)
		{
			Notice(string.Format(format,args));
		}
		
		public void WarnFormat(string format, params object[] args)
		{
			LogWrapper.WarnFormat(format, args);
		}
		
		public void ErrorFormat(string format, params object[] args)
		{
			LogWrapper.ErrorFormat(format, args);
		}
		
		public void FatalFormat(string format, params object[] args)
		{
			LogWrapper.FatalFormat(format, args);
		}
		
        public void Register(LogAware logAware)
        {
            LogWrapper.Register(logAware);
            logAware.RefreshLogLevel();
        }


        public void DebugFormat(LogMessage format)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format);
            }
            else
            {
            }
        }

#region DebugFormat
        public void DebugFormat<T>(LogMessage format, T arg)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg);
            }
            else
            {
                SerializeArgument(arg);
            }
        }

        public void DebugFormat<T1, T2>(LogMessage format, T1 arg1, T2 arg2)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
            }
        }

        public void DebugFormat<T1, T2, T3>(LogMessage format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
            }
        }

        public void DebugFormat<T1, T2, T3, T4>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4, arg5);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5, T6>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4, arg5, arg6);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5, T6, T7>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, args);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
                for (var x = 0; x < args.Length; x++)
                {
                    SerializeArgument(args[x]);
                }
            }
        }

        public void DebugFormat(string format)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format);
            }
            else
            {
            }
        }

        public void DebugFormat<T>(string format, T arg)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg);
            }
            else
            {
                SerializeArgument(arg);
            }
        }

        public void DebugFormat<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
            }
        }

        public void DebugFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
            }
        }

        public void DebugFormat<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4, arg5);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4, arg5, arg6);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (allowDebugging || !genericSerialized)
            {
                DebugInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
            }
        }

        public void DebugFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, args);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
                for (var x = 0; x < args.Length; x++)
                {
                    SerializeArgument(args[x]);
                }
            }
        }

#endregion

#region TraceFormat
        public void TraceFormat(LogMessage format)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format);
            }
            else
            {
            }
        }

        public void TraceFormat<T>(LogMessage format, T arg)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg);
            }
            else
            {
                SerializeArgument(arg);
            }
        }

        public void TraceFormat<T1, T2>(LogMessage format, T1 arg1, T2 arg2)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
            }
        }

        public void TraceFormat<T1, T2, T3>(LogMessage format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
            }
        }

        public void TraceFormat<T1, T2, T3, T4>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5, T6>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5, T6, T7>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, args);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
                for (var x = 0; x < args.Length; x++)
                {
                    SerializeArgument(args[x]);
                }
            }
        }

        public void TraceFormat(string format)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format);
            }
            else
            {
            }
        }

        public void TraceFormat<T>(string format, T arg)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg);
            }
            else
            {
                SerializeArgument(arg);
            }
        }

        public void TraceFormat<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
            }
        }

        public void TraceFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
            }
        }

        public void TraceFormat<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
            }
        }

        public void TraceFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, args);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
                for (var x = 0; x < args.Length; x++)
                {
                    SerializeArgument(args[x]);
                }
            }
        }

#endregion

#region VerboseFormat
        public void VerboseFormat(LogMessage format)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format);
            }
            else
            {
            }
        }

        public void VerboseFormat<T>(LogMessage format, T arg)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg);
            }
            else
            {
                SerializeArgument(arg);
            }
        }

        public void VerboseFormat<T1, T2>(LogMessage format, T1 arg1, T2 arg2)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
            }
        }

        public void VerboseFormat<T1, T2, T3>(LogMessage format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5, T6>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5, arg6);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5, T6, T7>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args)
        {
            if (allowDebugging || !genericSerialized)
            {
                TraceInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, args);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
                for (var x = 0; x < args.Length; x++)
                {
                    SerializeArgument(args[x]);
                }
            }
        }

        public void VerboseFormat(string format)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format);
            }
            else
            {
            }
        }

        public void VerboseFormat<T>(string format, T arg)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg);
            }
            else
            {
                SerializeArgument(arg);
            }
        }

        public void VerboseFormat<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
            }
        }

        public void VerboseFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5, arg6);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
            }
        }

        public void VerboseFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args)
        {
            if (allowDebugging || !genericSerialized)
            {
                VerboseInternal(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }
            else
            {
                SerializeArgument(arg1);
                SerializeArgument(arg2);
                SerializeArgument(arg3);
                SerializeArgument(arg4);
                SerializeArgument(arg5);
                SerializeArgument(arg6);
                SerializeArgument(arg7);
                SerializeArgument(arg8);
                for( var x=0; x<args.Length; x++)
                {
                    SerializeArgument(args[x]);
                }
            }
        }
#endregion

    }
}
