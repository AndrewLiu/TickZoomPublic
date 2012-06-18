#region Copyright
/*
 * Copyright 2008 M. Wayne Walter
 * Software: TickZoom Trading Platform
 * User: Wayne Walter
 * 
 * You can use and modify this software under the terms of the
 * TickZOOM General Public License Version 1.0 or (at your option)
 * any later version.
 * 
 * Businesses are restricted to 30 days of use.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * TickZOOM General Public License for more details.
 *
 * You should have received a copy of the TickZOOM General Public
 * License along with this program.  If not, see
 * <http://www.tickzoom.org/wiki/Licenses>.
 */
#endregion

using System;
using System.IO;

namespace TickZoom.Api
{
    /// <summary>
    /// Description of TickDOM.
    /// </summary>
    unsafe public class TickBox : TickIO
    {
        private TimeStamp time;
        private TickBinary tick;
        private double strike;
        private double bid;
        private double ask;
        private double price;
		
        public void Copy(TickIO other) {
            throw new NotImplementedException();
        }
		
        public TickBinary Tick {
            get { return tick; }
            set { tick = value;
                if( tick.IsTrade)
                {
                    price = tick.Price.ToDouble();
                }
            }
        }

        public void init(TickBinary tick) {
            throw new NotImplementedException();
        }
		
        public void init(TickIO tick) {
            throw new NotImplementedException();
        }
		
        public void init(TickIO tick, byte contentMask){
            throw new NotImplementedException();
        }
		
        public void init(TimeStamp utcTime, double dBid, double dAsk) {
            throw new NotImplementedException();
        }

        public void init(TimeStamp utcTime, double price, int size) {
            throw new NotImplementedException();
        }

        public void init(TimeStamp utcTime, byte side, double price, int size) {
            init(utcTime, side, price, size);
        }
		
        public void init(TimeStamp utcTime, byte side, double dPrice, int size, double dBid, double dAsk) {
            init(utcTime, side, dPrice, size, dBid, dAsk);
        }

        public void init(TimeStamp utcTime, byte side, double price, int size, double dBid, double dAsk, short[] bidSize, short[] askSize) {
            init(utcTime, side, price, size, dBid, dAsk, bidSize, askSize);
        }

        public int BidDepth {
            get { return tick.BidDepth; }
        }
		
        public int AskDepth {
            get { return tick.AskDepth; }
        }
		
        public override string ToString() {
            return tick.ToString();
        }
		
        public int FromFileVersion4(BinaryReader reader) {
            throw new NotImplementedException();
        }

        public int FromFileVersion3(BinaryReader reader) {
            throw new NotImplementedException();
        }
		
        public int FromFileVersion2(BinaryReader reader) {
            throw new NotImplementedException();
        }
		
        public int FromFileVersion1(BinaryReader reader) {
            throw new NotImplementedException();
        }
		
        public int FromReader(byte version, BinaryReader reader) {
            throw new NotImplementedException();
        }
		
        public int FromReader(MemoryStream reader) {
            throw new NotImplementedException();
        }

        public void ResetCompression()
        {
            throw new NotImplementedException();
        }
		
        private byte calcDigits(int num) {
            byte digits = 0;
            while(num!=0) {
                num/=10;
                digits++;;
            }
            return digits;
        }
		
        public double Strike
        {
            get
            {
                strike = tick.Strike.ToDouble();
                return strike;
            }
        }

        public double Bid
        {
            get
            {
                bid = tick.Bid.ToDouble();
                return bid; }
        }
		
        public double Ask {
            get
            {
                ask = tick.Ask.ToDouble();
                return ask;
            }
        }
		
        public TradeSide Side {
            get { return (TradeSide) tick.Side; }
        }
		
        public double Price {
            get { return price; }
        }
		
        public int Size {
            get { return tick.Size; }
        }
		
        public int Volume {
            get { return tick.Size; }
        }
		
        public short AskLevel(int level) {
            fixed (ushort* ptr = tick.DepthAskLevels)
            {
                return (short)ptr[level];
            }
        }
		
        public void SetAskLevel(int level, short size)
        {
            fixed (ushort* ptr = tick.DepthAskLevels)
            {
                ptr[level] = (ushort) size;
            }
        }
		
        public void SetBidLevel(int level, short size)
        {
            fixed (ushort* ptr = tick.DepthBidLevels)
            {
                ptr[level] = (ushort) size;
            }
        }
		
        public short BidLevel(int level) {
            return BidLevel(level);
        }
		
        //public int CompareTo(TickBox other)
        //{
        //    return tick.CompareTo(ref other.tick);
        //}
		
        public override int GetHashCode()
        {
            return tick.GetHashCode();
        }

        public override bool Equals(object other)
        {
            TickBox box = other as TickBox;
            return false;
        }
		
        public TimeStamp UtcTime {
            get { return new TimeStamp(tick.UtcTime); }
        }

        public long lUtcTime
        {
            get { return tick.UtcTime; }
        }

        public TimeStamp UtcOptionExpiration
        {
            get { return new TimeStamp(tick.UtcOptionExpiration); }
        }

        public int DomLevels {
            get { return tick.AskDepth; }
        }
		
        public bool IsTrade {
            get { return tick.IsTrade; }
        }

        public bool IsOption
        {
            get { return tick.IsOption; }
        }

        public OptionType OptionType
        {
            get { return tick.OptionType; }
        }

        public bool IsQuote
        {
            get { return tick.IsQuote; }
        }

        public long lStrike
        {
            get { return tick.Strike; }
            set { throw new NotImplementedException(); }
        }

        public long lBid
        {
            get { return tick.Bid; }
            set { throw new NotImplementedException(); }
        }

        public long lAsk
        {
            get { return tick.Ask; }
            set { throw new NotImplementedException(); }
        }
		
        public long lPrice {
            get { return tick.Price; }
            set { throw new NotImplementedException(); }
        }
		
        public long lSymbol {
            get { return tick.Symbol; }
            set { throw new NotImplementedException(); }
        }
		
        public string Symbol {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
		
        public bool IsSimulateTicks {
            get { return tick.IsSimulateTicks; }
            set { tick.IsSimulateTicks = value; }
        }
		
        public object ToPosition() {
            return tick.UtcTime;
        }
		
        public int Length {
            get {
                throw new NotImplementedException();
            }
        }
		
        public void ToWriter(MemoryStream memory)
        {
            throw new NotImplementedException();
        }
		
        public void Compress(MemoryStream memory)
        {
            throw new NotImplementedException();
        }
		
        public TickBinary Extract()
        {
            return tick;
        }
		
        public void Initialize()
        {
            throw new NotImplementedException();
        }
		
        public void SetTime(TimeStamp utcTime)
        {
            throw new NotImplementedException();
        }

        public void SetOption(OptionType optionType, double strikePrice, TimeStamp utcOptionExpiration)
        {
            throw new NotImplementedException();
        }

        public void SetQuote(double dBid, double dAsk)
        {
            throw new NotImplementedException();
        }
		
        public void SetQuote(double dBid, double dAsk, short bidSize, short askSize)
        {
            throw new NotImplementedException();
        }
		
        public void SetTrade(double price, int size)
        {
            throw new NotImplementedException();
        }
		
        public void SetTrade(TradeSide side, double price, int size)
        {
            throw new NotImplementedException();
        }
		
        public void SetDepth(short[] bidSize, short[] askSize)
        {
            throw new NotImplementedException();
        }
		
        public void Copy(TickIO other, byte contentMask)
        {
            throw new NotImplementedException();
        }
		
        void ReadWritable<TickBinary>.Inject(TickBinary tick)
        {
            throw new NotImplementedException();
        }
		
        void ReadWritable<TickBinary>.SetSymbol(long lSymbol)
        {
            throw new NotImplementedException();
        }
		
        public bool HasDepthOfMarket {
            get {
                throw new NotImplementedException();
            }
        }

        public TimeStamp Time
        {
            get { return time; }
            set { time = value; }
        }

        public byte DataVersion
        {
            get { return 1; }
        }
    }
}