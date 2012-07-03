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

namespace TickZoom.Provider.MBTQuotes
{
    public class MessageMbtQuotes : Message {
        private const byte EndOfField = 59;
        private const byte EndOfMessage = 10;
        private const byte DecimalPoint = 46;
        private const byte EqualSign = 61;
        private const byte ZeroChar = 48;
        private const int maxSize = 4096;
        private static readonly Log log = Factory.SysLog.GetLogger(typeof(MessageMbtQuotes));
        private static readonly bool trace = log.IsTraceEnabled;
        private MemoryStream data = new MemoryStream(8*1024);
        private BinaryReader dataIn;
        private BinaryWriter dataOut;
        private static int packetIdCounter = 0;
        private int id = 0;
        private StringBuilder symbol = new StringBuilder();
        private StringBuilder optionSymbol = new StringBuilder();
        private StringBuilder feedType = new StringBuilder();
        private double bid;
        private OptionType optionType;
        private double strike;
        private long utcOptionExpiration;
        private double ask;
        private int askSize;
        private int bidSize;
        private StringBuilder time = new StringBuilder();
        private StringBuilder date = new StringBuilder();
        private long tickUtcTime = long.MaxValue;
        private long _sendUtcTime = long.MaxValue;
        private long _recvUtcTime = long.MaxValue;
        private double last;
        private int century;

        public MessageMbtQuotes()
        {
            var currentTime = Factory.Parallel.UtcNow;
            century = currentTime.Year / 100 * 100;
            dataIn = new BinaryReader(data, Encoding.ASCII);
            dataOut = new BinaryWriter(data, Encoding.ASCII);
            Clear();
        }
		
        public void Clear()
        {
            id = ++packetIdCounter;
            data.Position = 0;
            data.SetLength(0);
            symbol.Length = 0;
            feedType.Length = 0;
            bid = 0D;
            ask = 0D;
            askSize = 0;
            bidSize = 0;
            time.Length = 0;
            date.Length = 0;
            tickUtcTime = long.MaxValue;
            _sendUtcTime = long.MaxValue;
            _recvUtcTime = long.MaxValue;
            last = 0D;
            date.Length = 0;
            time.Length = 0;
            tickUtcTime = long.MaxValue;
        }

        public double Last
        {
            get { return last; }
        }
        private int lastSize;
		
        public int LastSize {
            get { return lastSize; }
        }
        private int condition;
		
        public int Condition {
            get { return condition; }
        }
        private int salesType;
        public int SalesType
        {
            get { return salesType;  }
        }

        private int status;
		
        public int Status {
            get { return status; }
        }
        private int type;
		
        public int Type {
            get { return type; }
        }
        private char messageType;
		
        public char MessageType {
            get { return messageType; }
        }
		
        public void SetReadableBytes(int bytes) {
            if( trace) log.TraceFormat("SetReadableBytes({0})", bytes);
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
		
        public int Remaining {
            get { return Length - Position; }
        }
		
        public bool IsFull {
            get { return Length > 4096; }
        }
		
        public bool HasAny {
            get { return Length > 0; }
        }
		
        public unsafe void CreateHeader(int counter) {
        }

        private readonly char[] semiColon = new char[] { ';' };
        private readonly char[] equalSign = new char[] { '=' };
        private readonly char[] hyphen = new char[] { '-' };
        private readonly char[] slash = new char[] { '/' };
        string[] curlyBraces = new string[] { "{", "}" };
        private readonly char[] optionTypeChar = new char[] { 'C', 'P' };
        private unsafe void ParseData()
        {
            if (trace)
            {
                data.Position = 0;
                var messageText = new string(dataIn.ReadChars(Remaining));
                log.TraceFormat("ParseData(): {0}", messageText);
            }
            _sendUtcTime = Factory.Parallel.UtcNow.Internal;
            data.Position = 2;
            fixed( byte *bptr = data.GetBuffer()) {
                messageType = (char) *bptr;
                byte *ptr = bptr + data.Position;
                byte *end = bptr + data.Length;
                while( ptr - bptr < data.Length) {
                    int key = GetKey(ref ptr, end);
                    switch( key) {
                        case 2034:
                            GetString(symbol, ref ptr, end);
                            var test = symbol.ToString();
                            break;
                        case 1003:
                            if( messageType == '4') {
                                GetString(optionSymbol, ref ptr, end);
                                //+XDE-120616P154.00
                                var symbolParts = optionSymbol.ToString().Split(hyphen);
                                optionType = symbolParts[1].Contains("C") ? OptionType.Call : OptionType.Put;
                                var dateStrike = symbolParts[1].Split(optionTypeChar);
                                strike = double.Parse(dateStrike[1]);
                                var dateString = dateStrike[0];
                                var year = dateString.Substring(0, 2);
                                var month = dateString.Substring(2, 2);
                                var day = dateString.Substring(4, 2);
                                var ts = new TimeStamp(century + int.Parse(year), int.Parse(month), int.Parse(day));
                                utcOptionExpiration = ts.Internal;
                            }
                            else
                            {
                                GetString(symbol, ref ptr, end);
                            }
                            break;
                        case 2000: // Type of Data
                            GetString( feedType, ref ptr, end);
                            break;
                        case 2014: // Time
                            GetString( time, ref ptr, end);
                            break;
                        case 2015: // Date
                            GetString( date, ref ptr, end);
                            break;
                        case 2003: // Bid
                            bid = GetDouble(ref ptr, end);
                            break;
                        case 2004: // Ask
                            ask = GetDouble(ref ptr, end);
                            break;
                        case 2005: // Bid Size
                            askSize = GetInt(ref ptr, end);
                            break;
                        case 2006: // Ask Size
                            bidSize = GetInt(ref ptr, end);
                            break;
                        case 2002: // Last Trade Price
                            last = GetDouble(ref ptr, end);
                            break;
                        case 2007: // Last Trade Size
                            lastSize = GetInt(ref ptr, end);
                            break;
                        case 2039: // Last Trade Size
                            salesType = GetInt(ref ptr, end);
                            break;
                        case 2082: // Condition
                            condition = GetInt(ref ptr, end);
                            break;
                        case 2083: // Status
                            status = GetInt(ref ptr, end);
                            break;
                        case 2084: // Type
                            type = GetInt(ref ptr, end);
                            break;
                        default:
                            SkipValue(ref ptr, end);
                            break;
                    }
                    if( *(ptr-1) == 10)
                    {
                        tickUtcTime = GetTickUtcTime();
                        return;
                    }
                }
            }
        }
		
        public bool TrySplit(MemoryStream buffer)
        {
            var bytes = buffer.GetBuffer();
            var length = (int)buffer.Length;
            var position = (int)buffer.Position;
            for (int i = position; i < length; i++)
            {
                if (bytes[i] == '\n')
                {
                    var size = i - position + 1;
                    data.Write(bytes, (int)position, size);
                    buffer.Position += size;
                    ParseData();
                    return true;
                }
            }
            return false;
        }
		
        private unsafe int GetKey( ref byte *ptr, byte *end) {
            byte *bptr = ptr;
            int val = *ptr - 48;
            while (*(++ptr) != EqualSign && *ptr != EndOfMessage && ptr < end) {
                val = val * 10 + *ptr - 48;
            }
            ++ptr;
            Position += (int) (ptr - bptr);
            return val;
        }
        
        private unsafe int GetInt( ref byte *ptr, byte *end) {
            byte *bptr = ptr;
            int val = *ptr - 48;
            while (*(++ptr) != EndOfField && *ptr != EndOfMessage && ptr < end) {
                val = val * 10 + *ptr - 48;
            }
            ++ptr;
            Position += (int) (ptr - bptr);
            return val;
        }
		
        private unsafe double GetDouble( ref byte *ptr, byte *end) {
            byte *bptr = ptr;
            int val = 0;
            while (*(ptr) != DecimalPoint && *ptr != EndOfField && *ptr != EndOfMessage && ptr < end) {
                val = val * 10 + *ptr - 48;
                ++ptr;
            }
            if( *ptr == EndOfField || *ptr == EndOfMessage) {
                ++ptr;
                Position += (int)(ptr - bptr);
                return val;
            } else {
                ++ptr;
                int divisor = 10;
                int fract = *ptr - 48;
                int decimals = 1;
                while (*(++ptr) != EndOfField && *ptr != EndOfMessage && ptr < end) {
                    fract = fract * 10 + *ptr - 48;
                    divisor *= 10;
                    decimals++;
                }
                ++ptr;
                Position += (int) (ptr - bptr);
                double result = val + Math.Round((double)fract/divisor,decimals);
                return result;
            }
        }
		
        private unsafe void GetString( StringBuilder sb, ref byte* ptr, byte *end)
        {
            sb.Length = 0;
            byte *sptr = ptr;
            while (*(++ptr) != 59 && *ptr != 10 && ptr < end);
            int length = (int) (ptr - sptr);
            ++ptr;
            sb.Append(dataIn.ReadChars(length));
            data.Position++;
        }
        
        private unsafe void SkipValue( ref byte* ptr, byte *end) {
            byte *bptr = ptr;
            while (*(++ptr) != 59 && *ptr != 10 && ptr < end);
            ++ptr;
            Position += (int) (ptr - bptr);
        }

        public unsafe int Position { 
            get { return (int) data.Position; }
            set { data.Position = value; }
        }
		
        public bool IsComplete {
            get {
                var bytes = data.GetBuffer();
                var end = (int) data.Length - 1;
                return bytes[end] == '\n';
            }
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
		
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("MessageMbtQuotes: Position " + data.Position + ", length " + data.Length + "  ");
            sb.Append(ASCIIEncoding.UTF8.GetString(data.GetBuffer(),0,(int)data.Length));
            var result = sb.ToString();
            result = result.Replace("\n", @"\n");
            return result.Replace("\r", @"\r");
            //int offset = (int)data.Position;
            //while( offset < data.Length) {
            //    int rowSize = (int) Math.Min(16,data.Length-offset);
            //    byte[] bytes = new byte[rowSize];
            //    Array.Copy(data.GetBuffer(),offset,bytes,0,rowSize);
            //    for( int i = 0; i<bytes.Length; i++) {
            //        sb.Append(bytes[i].ToString("X").PadLeft(2,'0'));
            //        sb.Append(" ");
            //    }
            //    offset += rowSize;
            //    sb.AppendLine();
            //    sb.AppendLine(ASCIIEncoding.UTF8.GetString(bytes));
            //}))
            //return sb.ToString();
        }
		
        public int MaxSize {
            get { return maxSize; }
        }

        public long GetTickUtcTime()
        {
            if (date.Length == 0)
            {
                return long.MaxValue;
                //throw new InvalidOperationException("date field in the quote message was empty.");
            }
            if (time.Length == 0)
            {
                return long.MaxValue;
                //throw new InvalidOperationException("time field in the quote message was empty.");
            }
            var strings = date.ToString().Split(new char[] { '/' });
            var tempDate = strings[2] + "/" + strings[0] + "/" + strings[1];
            return new TimeStamp(tempDate + " " + time).Internal;
        }

        public long TickUtcTime
        {
            get {
                if( tickUtcTime == long.MaxValue)
                {
                    throw new InvalidOperationException("tickUtcTime was never parsed from the message.");
                }
                return tickUtcTime;
            }
        }

        public long SendUtcTime
        {
            get { return _sendUtcTime; }
            set { _sendUtcTime = value; }
        }
				
        public string Symbol {
            get { return symbol.ToString(); }
        }
		
        public double Bid {
            get { return bid; }
        }
		
        public double Ask {
            get { return ask; }
        }
		
        public int AskSize {
            get { return askSize; }
        }
		
        public int BidSize {
            get { return bidSize; }
        }
		
        public string Time {
            get { return time.ToString(); }
        }
		
        public string Date {
            get { return date.ToString(); }
        }
		
        public string FeedType {
            get { return feedType.ToString(); }
        }

        public long RecvUtcTime
        {
            get { return _recvUtcTime; }
            set { _recvUtcTime = value; }
        }

        public OptionType OptionType
        {
            get { return optionType; }
        }

        public double Strike
        {
            get { return strike; }
        }

        public long UtcOptionExpiration
        {
            get { return utcOptionExpiration; }
        }
    }
}