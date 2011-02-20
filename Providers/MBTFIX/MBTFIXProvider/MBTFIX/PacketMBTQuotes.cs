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
using System.Text;

using TickZoom.Api;

namespace TickZoom.MBTQuotes
{
	public class PacketMBTQuotes : Packet {
		private const byte EndOfField = 59;
		private const byte EndOfMessage = 10;
		private const byte DecimalPoint = 46;
		private const byte EqualSign = 61;
		private const byte ZeroChar = 48;
		private const int maxSize = 4096;
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(PacketMBTQuotes));
		private static readonly bool trace = log.IsTraceEnabled;
		private MemoryStream data = new MemoryStream();
		private BinaryReader dataIn;
		private BinaryWriter dataOut;
		private static int packetIdCounter = 0;
		private int id = 0;
		
		public PacketMBTQuotes() {
			id = ++packetIdCounter;
			dataIn = new BinaryReader(data, Encoding.ASCII);
			dataOut = new BinaryWriter(data, Encoding.ASCII);
			Clear();
		}
		
		public void SetReadableBytes(int bytes) {
			if( trace) log.Trace("SetReadableBytes(" + bytes + ")");
			data.SetLength( data.Position + bytes);
		}

		public void Verify() {
			
		}
		
		public void Clear() {
			data.Position = 0;
			data.SetLength(0);
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
			get { return Length - 0 > 0; }
		}
		
		public unsafe void CreateHeader(int counter) {
		}
		
		private int FindSplitAt() {
			byte[] bytes = data.GetBuffer();
			int length = (int) data.Length;
			for( int i=0; i<length; i++) {
				if( bytes[i] == '\n') {
					return i+1;
				}
			}
			return 0;
		}
		
		public bool TrySplit(MemoryStream other) {
			int splitAt = FindSplitAt();
			if( splitAt == data.Length) {
				data.Position = data.Length;
				return false;
			} else if( splitAt > 0) {
				other.Write(data.GetBuffer(), splitAt, (int) (data.Length - splitAt));
				data.SetLength( splitAt);
				return true;
			} else {
				return false;
			}
		}
		
		public unsafe int GetKey( ref byte *ptr) {
			byte *bptr = ptr;
	        int val = *ptr - 48;
	        while (*(++ptr) != EqualSign && *ptr != EndOfMessage) {
	        	val = val * 10 + *ptr - 48;
	        }
	        ++ptr;
	        Position += (int) (ptr - bptr);
	        return val;
		}
        
		public unsafe int GetInt( ref byte *ptr) {
			byte *bptr = ptr;
	        int val = *ptr - 48;
	        while (*(++ptr) != EndOfField && *ptr != EndOfMessage) {
	        	val = val * 10 + *ptr - 48;
	        }
	        ++ptr;
	        Position += (int) (ptr - bptr);
	        return val;
		}
		
		public unsafe double GetDouble( ref byte *ptr) {
			byte *bptr = ptr;
	        int val = 0;
	        while (*(ptr) != DecimalPoint && *ptr != EndOfField && *ptr != EndOfMessage) {
	        	val = val * 10 + *ptr - 48;
	        	++ptr;
	        }
	        if( *ptr == EndOfField || *ptr == EndOfMessage) {
		        Position += (int) (ptr - bptr);
		        return val;
	        } else {
		        ++ptr;
		        int divisor = 10;
		        int fract = *ptr - 48;
		        int decimals = 1;
		        while (*(++ptr) != EndOfField && *ptr != EndOfMessage) {
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
		
		public unsafe string GetString( ref byte* ptr) {
			byte *sptr = ptr;
			while (*(++ptr) != 59 && *ptr != 10);
	        int length = (int) (ptr - sptr);
	        ++ptr;
			var result = new string(dataIn.ReadChars(length));
			data.Position++;
			return result;
		}
        
		public unsafe void SkipValue( ref byte* ptr) {
			byte *bptr = ptr;
	        while (*(++ptr) != 59 && *ptr != 10);
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
			sb.AppendLine("PacketMBTQuotes: Position " + data.Position + ", length " + data.Length);
			int offset = (int)data.Position;
			while( offset < data.Length) {
				int rowSize = (int) Math.Min(16,data.Length-offset);
				byte[] bytes = new byte[rowSize];
				Array.Copy(data.GetBuffer(),offset,bytes,0,rowSize);
				for( int i = 0; i<bytes.Length; i++) {
					sb.Append(bytes[i].ToString("X").PadLeft(2,'0'));
					sb.Append(" ");
				}
				offset += rowSize;
				sb.AppendLine();
				sb.AppendLine(ASCIIEncoding.UTF8.GetString(bytes));
			}
			return sb.ToString();
		}
		
		public int MaxSize {
			get { return maxSize; }
		}
	}
}