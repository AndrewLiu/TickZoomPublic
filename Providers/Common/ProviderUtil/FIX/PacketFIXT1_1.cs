﻿#region Copyright
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

using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using TickZoom.Api;

namespace TickZoom.FIX
{
    public unsafe class MessageFIXT1_1 : Message
    {
		private const byte EndOfField = 1;
		private const byte NegativeSign = (byte) '-';
		private const byte DecimalPoint = 46;
		private const byte EqualSign = 61;
		private const byte ZeroChar = 48;
		private const int maxSize = 4096;
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(MessageFIXT1_1));
		private static readonly bool debug = log.IsDebugEnabled;
		private static readonly bool trace = log.IsTraceEnabled;
		private MemoryStream data = new MemoryStream();
		private BinaryReader dataIn;
		private BinaryWriter dataOut;
		private static int packetIdCounter = 0;
        private int heartBeatInterval = 0;
        private string encryption = null;
        private int id = 0;
		private long sendUtcTime;
        private long recvUtcTime;
        private GCHandle handle;
		private byte* ptr;
		private byte* end;
		private string version = null;
		private int begSeqNum;
		private int endSeqNum;
		private int length = 0;
		private string messageType = null;
		private string sender = null;
		private string target = null;
		private int sequence = 0;
		private string timeStamp = null;
		private int checkSum = 0;
		private bool isPossibleDuplicate = false;
		public static bool IsQuietRecovery = false;
        private int newSeqNum;
        private bool isGapFill;
        private bool isResetSeqNum;
		
		public MessageFIXT1_1()
		{
			dataIn = new BinaryReader(data, Encoding.ASCII);
			dataOut = new BinaryWriter(data, Encoding.ASCII);
            Clear();
		}

        public virtual void Clear()
        {
			id = ++packetIdCounter;
            data.Position = 0;
            data.SetLength(0);
		    sendUtcTime = 0L;
            recvUtcTime = 0L;
            if (handle.IsAllocated)
            {
                handle.Free();
            }
            heartBeatInterval = 0;
            isResetSeqNum = false;
            encryption = null;
            ptr = null;
		    end = null;
		    version = null;
		    begSeqNum = 0;
		    endSeqNum = 0;
		    length = 0;
		    messageType = null;
		    sender = null;
		    target = null;
		    sequence = 0;
		    timeStamp = null;
		    checkSum = 0;
		    isPossibleDuplicate = false;
        }

        public void SetReadableBytes(int bytes)
        {
			if( trace) log.Trace("SetReadableBytes(" + bytes + ")");
			data.SetLength( data.Position + bytes);
		}
	
		public void Verify() {
			
		}
		
		public void BeforeWrite() {
			data.Position = 0;
			data.SetLength(0);
		}
		
		public void BeforeRead() {
			data.Position = 0;
		}
		
		public override string ToString()
		{
		    var position = data.Position;
			data.Position = 0;
			string response = new string(dataIn.ReadChars((int)data.Length));
			var result = response.Replace(FIXTBuffer.EndFieldStr,"  ");
		    data.Position = position;
		    return result;
		} 
		
		public int Remaining {
			get { return Length - Position; }
		}
		
		public bool IsFull {
			get { return Length > 0; }
		}
		
		public bool HasAny {
			get { return Length - 0 > 0; }
		}
		
		public unsafe void CreateHeader(int counter) {
		}
		
		private int FindSplitAt() {
			if( trace) log.Trace("Processing Keys: " + this);
		    data.Position = 0;
			handle = GCHandle.Alloc(data.GetBuffer(), GCHandleType.Pinned);
			ptr = (byte*)handle.AddrOfPinnedObject();	
			end = ptr + data.Length;
			int key;
			while( NextKey( out key)) {
				if( trace) log.Trace("HandleKey("+key+")");
				HandleKey(key);
				if( key == 10 ) {
					isComplete = true;
					if( data.Position == data.Length) {
						return 0;
					} else if( data.Position < data.Length) {
						if( trace) log.Trace("Splitting message at " + data.Position);
						return (int) data.Position;
					}
				}
			}
			// Never found a complete checksum tag so we need more bytes.
			data.Position = data.Length;
			isComplete = false;
			return 0;
		}
		
		private bool isComplete;
		
		public bool IsComplete {
			get { return isComplete; }
		}
		
		
		private void LogMessage() {
			if( trace &&
			   !IsQuietRecovery &&
			   messageType != "1" && messageType != "0") {
				log.Trace("Reading message: \n" + this);
			}
		}
		
		public bool TrySplit(MemoryStream other) {
			bool result = false;
			int splitAt = FindSplitAt();
			if( splitAt > 0) {
				other.Write(data.GetBuffer(), splitAt, (int)data.Length - splitAt);
				data.Position = splitAt;
				data.SetLength( splitAt);
				result = true;
			} else {
				LogMessage();
				result = false;
			}
			return result;
		}
		
		protected unsafe bool GetKey(out int val) {
			byte *bptr = ptr;
	        val = *ptr - ZeroChar;
	        while (*(++ptr) != EqualSign) {
	        	if( ptr >= end) return false;
	        	val = val * 10 + *ptr - ZeroChar;
	        }
	        ++ptr;
	        Position += (int) (ptr - bptr);
	        return true;
		}
	        
		protected unsafe bool GetInt(out int val) {
			byte *bptr = ptr;
			bool negative = *ptr == NegativeSign;
			if( negative) {
				++ptr;
			}
	        val = *ptr - ZeroChar;
	        while (*(++ptr) != EndOfField) {
	        	if( ptr >= end) return false;
	        	val = val * 10 + *ptr - ZeroChar;
	        }
	        ++ptr;
	        Position += (int) (ptr - bptr);
	        if( negative) val *= -1;
			if( trace) log.Trace("int = " + val);
	        return true;
		}
		
		protected unsafe bool GetDouble(out double result) {
			byte *bptr = ptr;
	        int val = 0;
	        result = 0D;
	        while (*(ptr) != DecimalPoint && *(ptr) != EndOfField) {
	        	if( ptr >= end) return false;
	        	val = val * 10 + *ptr - ZeroChar;
	        	++ptr;
	        }
	        if( *(ptr) == EndOfField) {
		        ++ptr;
		        Position += (int) (ptr - bptr);
				result = val;
				if( trace) log.Trace("double = " + result);
		        return true;
	        } else {
		        ++ptr;
		        int divisor = 10;
		        int fract = *ptr - ZeroChar;
		        while (*(++ptr) != EndOfField) {
		        	if( ptr >= end) return false;
		        	fract = fract * 10 + *ptr - ZeroChar;
		        	divisor *= 10;
		        }
		        ++ptr;
		        Position += (int) (ptr - bptr);
		        result = val + (double) fract / divisor;
				if( trace) log.Trace("double = " + result);
				return true;
	        }
		}
		
		protected unsafe bool GetString(out string result) {
			byte *sptr = ptr;
			result = null;
			while (*ptr != EndOfField) {
                ptr++;
	        	if( ptr >= end) return false;
			}
	        int length = (int) (ptr - sptr);
	        ++ptr;
			result = new string(dataIn.ReadChars(length));
			data.Position++;
			if( trace) log.Trace("string = " + result);
			return true;
		}
	        
		protected unsafe bool SkipValue() {
			byte *bptr = ptr;
			while (*(++ptr) != EndOfField) {
	        	if( ptr >= end) return false;
			}
	        ++ptr;
	        int length = (int) (ptr - bptr);
	        Position += length;
			if( trace) log.Trace("skipping " + length + " bytes.");
			return true;
		}
		
		private bool NextKey(out int key) {
			key = 0;
			if( data.Position < data.Length) {
				return GetKey(out key);
			} else {
				return false;
			}
		}
		
		protected virtual bool HandleKey(int key) {
			bool result = false;
			switch( key) {
				case 7:
					result = GetInt(out begSeqNum);
					break;
				case 8:
					result = GetString(out version);
					break;
				case 9:
					result = GetInt(out length);
					break;
				case 16:
					result = GetInt(out endSeqNum);
					break;
				case 35:
					result = GetString(out messageType);
					break;
                case 36:
                    result = GetInt(out newSeqNum);
                    break;
                case 49:
					result = GetString(out sender);
					break;
				case 43:
					string value;
					result = GetString(out value);
					isPossibleDuplicate = value == "Y";
					break;
				case 56:
					result = GetString(out target);
					break;
				case 34:
					result = GetInt(out sequence);
					break;
				case 52:
					result = GetString(out timeStamp);
					if( result) {
						sendUtcTime = new TimeStamp(timeStamp).Internal;
					}
					break;
                case 98:
                    result = GetString(out encryption);
                    break;
                case 108:
                    result = GetInt(out heartBeatInterval);
                    break;
                case 123:
                    result = GetString(out value);
                    isGapFill = value == "Y";
                    break;
                case 141:
                    result = GetString(out value);
                    isResetSeqNum = value == "Y";
                    break;
                case 10:
					result = GetInt(out checkSum);
					break;
				default:
					result = SkipValue();
					break;
			}
			return result;
		}		
		
		public string Version {
			get { return version; }
		}
		
		public string MessageType {
			get { return messageType; }
		}
		
		public string Sender {
			get { return sender; }
		}
		
		public string Target {
			get { return target; }
		}
		
		public int Sequence {
			get { return sequence; }
		}

        public string Encryption
        {
            get { return encryption; }
        }

        public string TimeStamp
        {
			get { return timeStamp; }
		}

		public unsafe int Position { 
			get { return (int) data.Position; }
			set { data.Position = value; }
		}
		
		public int Length {
			get { return (int) data.Length; }
		}
		
		public BinaryReader DataIn {
			get { return dataIn; }
		}
		
		public BinaryWriter DataOut {
			get { return dataOut; }
		}
		
		public MemoryStream Data {
			get { return data; }
		}
		
		public int Id {
			get { return id; }
		}

        public int HeartBeatInterval
        {
            get { return heartBeatInterval; }
        }

        public int BegSeqNum
        {
			get { return begSeqNum; }
		}
		
		public int EndSeqNum {
			get { return endSeqNum; }
		}
		
		public int MaxSize {
			get { return maxSize; }
		}
		
		public long SendUtcTime {
			get { return sendUtcTime; }
			set { sendUtcTime = value; }
		}

	    public long RecvUtcTime
	    {
	        get { return recvUtcTime; }
	        set { recvUtcTime = value; }
	    }

        public bool IsPossibleDuplicate
        {
            get { return isPossibleDuplicate; }
        }

        public int NewSeqNum
        {
            get { return newSeqNum; }
        }

        public bool IsGapFill
        {
            get { return isGapFill; }
        }

        public bool IsResetSeqNum
        {
            get { return isResetSeqNum; }
        }
    }
}
