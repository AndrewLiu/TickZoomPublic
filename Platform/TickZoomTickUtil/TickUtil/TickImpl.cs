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
using TickZoom.Api;

namespace TickZoom.TickUtil
{
	/// <summary>
	/// Description of TickDOM.
	/// </summary>
	/// <inheritdoc/>
	unsafe public class TickImpl : TickIO
	{
		public const int minTickSize = 256;
		
		public static long ToLong( double value) { return value.ToLong(); }
		public static double ToDouble( long value) { return value.ToDouble(); }
		public static double Round( double value) { return value.Round() ; }
		private static string TIMEFORMAT = "yyyy-MM-dd HH:mm:ss.fffuuu";
		
		// Older formats were already multiplied by 1000.
		public const long OlderFormatConvertToLong = 1000000;
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(TickImpl));
		private static readonly bool trace = log.IsTraceEnabled;
		private static readonly bool debug = log.IsDebugEnabled;
        private static readonly bool verbose = log.IsVerboseEnabled;
		private DaylightSavings dst;

		TickBinary binary;
		TimeStamp localTime;
	    private TickSerializer tickSerializer;

		public void Initialize() {
			binary = default(TickBinary);
			localTime = default(TimeStamp);
		}
		
		/// <inheritdoc/>
		public void SetTime(TimeStamp utcTime)
		{
			binary.UtcTime = utcTime.Internal;
			if( dst == null) {
				if( binary.Symbol == 0) {
					throw new ApplicationException("Please call SetSymbol() prior to SetTime() method.");
				}
				SymbolInfo symbol = Factory.Symbol.LookupSymbol(binary.Symbol);
				dst = new DaylightSavings(symbol);
			}
			var offset = dst.GetOffset(utcTime);
			localTime = new TimeStamp(binary.UtcTime);
			localTime.AddSeconds(offset);
		}
		
		public void SetQuote(double dBid, double dAsk)
		{
			SetQuote( dBid.ToLong(), dAsk.ToLong());
		}
		
		public void SetQuote(double dBid, double dAsk, short bidSize, short askSize)
		{
			try {
				SetQuote( dBid.ToLong(), dAsk.ToLong(), bidSize, askSize);
			} catch( OverflowException) {
				throw new ApplicationException("Overflow exception occurred when converting either bid: " + dBid + " or ask: " + dAsk + " to long.");
			}
		}
		
		public void SetQuote(long lBid, long lAsk) {
			IsQuote=true;
			binary.Bid = lBid;
			binary.Ask = lAsk;
		}
		
		public void SetQuote(long lBid, long lAsk, short bidSize, short askSize)
		{
		    binary.SetQuote(lBid, lAsk, bidSize, askSize);
		}
		
		public void SetTrade(double price, int size)
		{
			SetTrade(TradeSide.Unknown,price.ToLong(),size);
		}
		
		public void SetTrade(TradeSide side, double price, int size)
		{
			SetTrade(side,price.ToLong(),size);
		}
		
		public void SetTrade(TradeSide side, long lPrice, int size) {
			IsTrade=true;
			binary.Side = (byte) side;
			binary.Price = lPrice;
			binary.Size = size;
		}

        public void SetOption(OptionType optionType, double strikePrice, TimeStamp utcOptionExpiration)
        {
            this.IsOption = true;
            binary.Strike = strikePrice.ToLong();
            binary.UtcOptionExpiration = utcOptionExpiration.Internal;
            this.OptionType = optionType;
        }

        public void SetDepth(ushort[] bidSize, ushort[] askSize)
        {
            binary.SetDepth(bidSize, askSize);
		}
		
		public void SetSymbol( long lSymbol) {
			binary.Symbol = lSymbol;
		}

        public void Copy(TickIO tick)
        {
            Copy(tick, tick.Extract().contentMask);
        }

	    public void Copy(TickIO tick, byte contentMask) {
            Initialize();
			SetSymbol(tick.lSymbol);
			SetTime(tick.UtcTime);
		    binary.contentMask = contentMask;
			if( binary.IsQuote) {
				SetQuote(tick.lBid, tick.lAsk);
			}
			if( binary.IsTrade) {
				SetTrade(tick.Side, tick.lPrice, tick.Size);
			}
            if (binary.IsOption)
            {
                var type = binary.OptionType;
                SetOption(type, tick.Strike, tick.UtcOptionExpiration);
            }
            if (binary.HasDepthOfMarket)
            {
                binary.CopyDepth(tick);
			}
		}
		
		public int BidDepth {
            get
            {
                return binary.BidDepth; 
			}
		}
		
		public int AskDepth {
			get { 
				return binary.AskDepth;
			}
		}
		
		public override string ToString() {
//			string output = Time.ToString(TIMEFORMAT) + " " +
//				(IsTrade ? (Side != TradeSide.Unknown ? Side.ToString() + "," : "") + Price.ToString(",0.000") + "," + binary.Size + ", " : "") +
//				Bid.ToString(",0.000") + "/" + Ask.ToString(",0.000") + " ";
			string output = Time.ToString(TIMEFORMAT) + " " +
				(IsTrade ? (Side != TradeSide.Unknown ? Side.ToString() + "," : "") + Price.ToString(",0.#########") + "," + binary.Size + ", " : "") +
				Bid.ToString(",0.#########") + "/" + Ask.ToString(",0.#########") + " ";
			for(int i=TickBinary.DomLevels-1; i>=0; i--) {
				if( i!=TickBinary.DomLevels-1) { output += ","; }
			    output += binary.BidLevel(i);
			}
			output += "|";
			for(int i=0; i<TickBinary.DomLevels; i++) {
				if( i!=0) { output += ","; }
			    output += binary.AskLevel(i);
			}
			return output;
		}

        public unsafe void ToWriter(MemoryStream writer)
        {
            if( tickSerializer == null)
            {
                tickSerializer = new TickSerializerDefault();
            }
            tickSerializer.ToWriter(ref binary, writer);
        }

        public void ResetCompression()
        {
            if (tickSerializer == null)
            {
                tickSerializer = new TickSerializerDefault();
            }
            tickSerializer.ResetCompression();
        }

        public int FromReader(MemoryStream reader)
        {
            if (tickSerializer == null)
            {
                tickSerializer = new TickSerializerDefault();
            }
            return tickSerializer.FromReader(ref binary, reader);
        }

		/// <summary>
		/// Old style FormatReader for legacy versions of TickZoom tck
		/// data files.
		/// </summary>
		public int FromReader(byte dataVersion, BinaryReader reader) {
            if (tickSerializer == null)
            {
                tickSerializer = new TickSerializerDefault();
            }
            return tickSerializer.FromReader(ref binary, dataVersion, reader);
		}
		
		public int CompareTo(ref TickImpl other)
		{
		    return binary.CompareTo(ref other.binary);
		}
		
        public double Strike
        {
            get { return binary.Strike.ToDouble(); }
        }

        public long lStrike
        {
            get { return binary.Strike; }
        }

        public double Bid  
        {
			get { return binary.Bid.ToDouble(); }
		}
		
		public double Ask {
			get { return binary.Ask.ToDouble(); }
		}
		
		public TradeSide Side {
			get { return (TradeSide) binary.Side; }
		}
		
		public double Price {
			get {
				if( IsTrade) {
					return binary.Price.ToDouble();
				} else {
					string msg = "Sorry. The Price property on a tick can only by accessed\n" +
					             "if it has trade data. Please, check the IsTrade property.";
					log.Error(msg);
					throw new ApplicationException(msg);
				}
			}
		}
		
		public int Size {
			get { return binary.Size; }
		}
		
		public int Volume {
			get { return Size; }
		}
		
		public ushort AskLevel(int level) {
            return binary.AskLevel(level);
		}
		
		public ushort BidLevel(int level)
		{
		    return binary.BidLevel(level);
		}
		
		public TimeStamp Time {
			get { return localTime; }
		}
		
        public TimeStamp UtcOptionExpiration
        {
            get { return new TimeStamp(binary.UtcOptionExpiration); }
        }

        public override int GetHashCode()
		{
			return base.GetHashCode();
		}
		
		public override bool Equals(object obj)
		{
			TickImpl other = (TickImpl) obj;
			return CompareTo(ref other) == 0;
		}
		
		public bool Equals(TickImpl other)
		{
			return CompareTo(ref other) == 0;
		}
		
		public long lBid {
			get { return binary.Bid; }
			set { binary.Bid = value; }
		}

		public long lAsk {
			get { return binary.Ask; }
            set { binary.Ask = value; }
		}
		
		public long lPrice {
			get { return binary.Price; }
		}
		
		public TimeStamp UtcTime {
			get { return new TimeStamp(binary.UtcTime); }
		}

		public long lSymbol {
			get { return binary.Symbol; }
		}
		
		public string Symbol {
			get { return binary.Symbol.ToSymbol(); }
		}
		
		public int DomLevels {
			get { return TickBinary.DomLevels; }
		}
		
		public bool IsQuote {
			get { return binary.IsQuote; }
			set { binary.IsQuote = value; }
		}
		
		public bool IsSimulateTicks {
            get { return binary.IsSimulateTicks; }
		    set { binary.IsSimulateTicks = value; }
		}
		
		public bool IsTrade {
			get { return binary.IsTrade; }
            set { binary.IsTrade = value; }
		}

        public bool IsOption
        {
            get { return binary.IsOption; }
            set { binary.IsOption = value; }
        }

        public OptionType OptionType
        {
            get { return binary.OptionType; }
            set { binary.OptionType = value; }
        }

        public bool HasDepthOfMarket
        {
			get { return binary.HasDepthOfMarket; }
			set { binary.HasDepthOfMarket = value; }
		}
		
		public object ToPosition() {
			return new TimeStamp(binary.UtcTime).ToString();
		}
		
		public int Sentiment {
			get { return 0; }
		}
		
		public TickBinary Extract()
		{
			return binary;
		}

		public void Inject(TickBinary tick) {
			binary = tick;
			SetTime( new TimeStamp(binary.UtcTime));
		}
		
		public bool IsRealTime {
			get { return false; }
			set { }
		}

	    public long lUtcTime
	    {
	        get { return binary.UtcTime; }
	    }

        public long lUtcOptionExpiration
        {
            get { return binary.UtcOptionExpiration; }
        }
    }
}
