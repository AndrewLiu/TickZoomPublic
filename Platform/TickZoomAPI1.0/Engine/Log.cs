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

namespace TickZoom.Api
{
    public interface Log 
    {
        [Obsolete("Use Notice method instead. ")]
        void WriteLine(string msg);
        
        [Obsolete("Log4Net handles this internally.")]
        void Clear();
        
        [Obsolete("Use Info or Debug methods instead. See those for more info.")]
        void WriteFile(string msg);
        
        bool HasLine {
        	get;
        }
        
        bool TryReadLine(out LogEvent[] logEvent);
        
 		string FileName {
			get;
			set;
		}

        void Assert(bool test);
        
        TimeStamp TimeStamp {
        	get;
        	set;
        }
        
        void Indent();
        
		void Outdent();
        
		/* Test if a level is enabled for logging */
		bool IsVerboseEnabled { get; }
		bool IsTraceEnabled { get; }
		bool IsDebugEnabled { get; }
		bool IsInfoEnabled { get; }
		bool IsNoticeEnabled { get; }
		bool IsWarnEnabled { get; }
		bool IsErrorEnabled { get; }
		bool IsFatalEnabled { get; }
		
		/* Log a message object */
		void Info(object message);
		void Notice(object message);
		void Warn(object message);
		void Error(object message);
		void Fatal(object message);
		
		/* Log a message object and exception */
        //void Verbose(object message, Exception t);
        //void Trace(object message, Exception t);
        //void Debug(string message, Exception t);
		void Info(object message, Exception t);
		void Notice(object message, Exception t);
		void Warn(object message, Exception t);
		void Error(object message, Exception t);
		void Fatal(object message, Exception t);
		
		/* Log a message string using the System.String.Format syntax */
        //void VerboseFormat(string format, params object[] args);
        //void TraceFormat(string format, params object[] args);
        //void DebugFormat(string format, params object[] args);
		void InfoFormat(string format, params object[] args);
		void NoticeFormat(string format, params object[] args);
		void WarnFormat(string format, params object[] args);
		void ErrorFormat(string format, params object[] args);
		void FatalFormat(string format, params object[] args);

        void DebugFormat(LogMessage format);
        void DebugFormat<T>(LogMessage format, T arg);
        void DebugFormat<T1,T2>(LogMessage format, T1 arg1, T2 arg2);
        void DebugFormat<T1,T2,T3>(LogMessage format, T1 arg1, T2 arg2, T3 arg3);
        void DebugFormat<T1,T2,T3,T4>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
        void DebugFormat<T1,T2,T3,T4,T5>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
        void DebugFormat<T1, T2, T3, T4, T5, T6>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
        void DebugFormat<T1, T2, T3, T4, T5, T6, T7>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
        void DebugFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
        void DebugFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args);
        void DebugFormat(string format);
        void DebugFormat<T>(string format, T arg);
        void DebugFormat<T1, T2>(string format, T1 arg1, T2 arg2);
        void DebugFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3);
        void DebugFormat<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
        void DebugFormat<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
        void DebugFormat<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
        void DebugFormat<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
        void DebugFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
        void DebugFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args);
        void TraceFormat(LogMessage format);
        void TraceFormat<T>(LogMessage format, T arg);
        void TraceFormat<T1, T2>(LogMessage format, T1 arg1, T2 arg2);
        void TraceFormat<T1, T2, T3>(LogMessage format, T1 arg1, T2 arg2, T3 arg3);
        void TraceFormat<T1, T2, T3, T4>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
        void TraceFormat<T1, T2, T3, T4, T5>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
        void TraceFormat<T1, T2, T3, T4, T5, T6>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
        void TraceFormat<T1, T2, T3, T4, T5, T6, T7>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
        void TraceFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
        void TraceFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args);
        void TraceFormat(string format);
        void TraceFormat<T>(string format, T arg);
        void TraceFormat<T1, T2>(string format, T1 arg1, T2 arg2);
        void TraceFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3);
        void TraceFormat<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
        void TraceFormat<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
        void TraceFormat<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
        void TraceFormat<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
        void TraceFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
        void TraceFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args);
        void VerboseFormat(LogMessage format);
        void VerboseFormat<T>(LogMessage format, T arg);
        void VerboseFormat<T1, T2>(LogMessage format, T1 arg1, T2 arg2);
        void VerboseFormat<T1, T2, T3>(LogMessage format, T1 arg1, T2 arg2, T3 arg3);
        void VerboseFormat<T1, T2, T3, T4>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
        void VerboseFormat<T1, T2, T3, T4, T5>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
        void VerboseFormat<T1, T2, T3, T4, T5, T6>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
        void VerboseFormat<T1, T2, T3, T4, T5, T6, T7>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
        void VerboseFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
        void VerboseFormat<T1, T2, T3, T4, T5, T6, T7, T8>(LogMessage format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args);
        void VerboseFormat(string format);
        void VerboseFormat<T>(string format, T arg);
        void VerboseFormat<T1, T2>(string format, T1 arg1, T2 arg2);
        void VerboseFormat<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3);
        void VerboseFormat<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
        void VerboseFormat<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
        void VerboseFormat<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
        void VerboseFormat<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
        void VerboseFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
        void VerboseFormat<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, params object[] args);

        void Register(LogAware logAware);

        List<ArgumentHandler> UniqueTypes { get; }
        List<FormatHandler> UniqueFormats { get; }
    }
    public class FormatHandler
    {
        public string Format;
        public long Count = 1;
    }
    public enum ArgumentType
    {
        Actual,
        ToString
    }
    public class ArgumentHandler
    {
        public ArgumentType ArgumentType;
        public long Count = 1;
        public bool IsUnknownType;
        public Type Type;
    }
}
