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
using System.Runtime.InteropServices;
using System.Text;

namespace TickZoom.Api
{
	/// <summary>
	/// Description of TickBinary.
	/// </summary>
	[CLSCompliant(false)]
    [StructLayout(LayoutKind.Explicit, Size=16, CharSet=CharSet.Ansi)]
	unsafe public struct TickBinary
	{
		public const int DomLevels = 5;
		public const int SymbolSize = 8;
		public const int minTickSize = 256;

        [FieldOffset(0)]  public long Symbol;
        [FieldOffset(8)]  public long Id;
		[FieldOffset(16)] public long UtcTime;
        [FieldOffset(24)] public long UtcOptionExpiration;
        [FieldOffset(32)] public long Strike;
        [FieldOffset(40)] public long Bid;
        [FieldOffset(48)] public long Ask;
        [FieldOffset(56)] public long Price;
        [FieldOffset(64)] public int Size;
        [FieldOffset(68)] private fixed ushort depthAskLevels[DomLevels];
        [FieldOffset(78)] private fixed ushort depthBidLevels[DomLevels];
        [FieldOffset(88)] public byte Side;
        [FieldOffset(89)] public byte contentMask;

        public bool IsQuote
        {
            get { return (contentMask & ContentBit.Quote) > 0; }
            set
            {
                if (value)
                {
                    contentMask |= ContentBit.Quote;
                }
                else
                {
                    contentMask &= (0xFF & ~ContentBit.Quote);
                }
            }
        }

        public bool IsSimulateTicks
        {
            get { return (contentMask & ContentBit.SimulateTicks) > 0; }
            set
            {
                if (value)
                {
                    contentMask |= ContentBit.SimulateTicks;
                }
                else
                {
                    contentMask &= (0xFF & ~ContentBit.SimulateTicks);
                }
            }
        }

        public bool IsTrade
        {
            get { return (contentMask & ContentBit.TimeAndSales) > 0; }
            set
            {
                if (value)
                {
                    contentMask |= ContentBit.TimeAndSales;
                }
                else
                {
                    contentMask &= (0xFF & ~ContentBit.TimeAndSales);
                }
            }
        }

        public bool IsOption
        {
            get { return (contentMask & ContentBit.Option) > 0; }
            set
            {
                if (value)
                {
                    contentMask |= ContentBit.Option;
                }
                else
                {
                    contentMask &= (0xFF & ~ContentBit.Option);
                }
            }
        }

        public OptionType OptionType
        {
            get { return (contentMask & ContentBit.CallOrPut) > 0 ? OptionType.Call : OptionType.Put; }
            set
            {
                if (value == OptionType.Call)
                {
                    contentMask |= ContentBit.CallOrPut;
                }
                else
                {
                    contentMask &= (0xFF & ~ContentBit.CallOrPut);
                }
            }
        }

        public bool HasDepthOfMarket
        {
            get { return (contentMask & ContentBit.DepthOfMarket) > 0; }
            set
            {
                if (value)
                {
                    contentMask |= ContentBit.DepthOfMarket;
                }
                else
                {
                    contentMask &= (0xFF & ~ContentBit.DepthOfMarket);
                }
            }
        }

        public void SetQuote(double dBid, double dAsk)
        {
            SetQuote(dBid.ToLong(), dAsk.ToLong());
        }

        public void SetQuote(double dBid, double dAsk, short bidSize, short askSize)
        {
            try
            {
                SetQuote(dBid.ToLong(), dAsk.ToLong(), bidSize, askSize);
            }
            catch (OverflowException)
            {
                throw new ApplicationException("Overflow exception occurred when converting either bid: " + dBid + " or ask: " + dAsk + " to long.");
            }
        }

        public void SetQuote(long lBid, long lAsk)
        {
            IsQuote = true;
            Bid = lBid;
            Ask = lAsk;
        }

        public void SetQuote(long lBid, long lAsk, short bidSize, short askSize)
        {
            IsQuote = true;
            HasDepthOfMarket = true;
            Bid = lBid;
            Ask = lAsk;
            fixed (ushort* b = depthBidLevels)
            fixed (ushort* a = depthAskLevels)
            {
                *b = (ushort)bidSize;
                *a = (ushort)askSize;
            }
        }

        public void SetTrade(double price, int size)
        {
            SetTrade(TradeSide.Unknown, price.ToLong(), size);
        }

        public void SetTrade(TradeSide side, double price, int size)
        {
            SetTrade(side, price.ToLong(), size);
        }

        public void SetTrade(TradeSide side, long lPrice, int size)
        {
            IsTrade = true;
            Side = (byte)side;
            Price = lPrice;
            Size = size;
        }

        public void SetOption(OptionType optionType, double strikePrice, TimeStamp utcOptionExpiration)
        {
            this.IsOption = true;
            Strike = strikePrice.ToLong();
            UtcOptionExpiration = utcOptionExpiration.Internal;
            this.OptionType = optionType;
        }

        public void SetDepth(ushort[] bidSize, ushort[] askSize)
        {
            HasDepthOfMarket = true;
            fixed (ushort* b = depthBidLevels)
            fixed (ushort* a = depthAskLevels)
            {
                for (int i = 0; i < TickBinary.DomLevels; i++)
                {
                    *(b + i) = bidSize[i];
                    *(a + i) = askSize[i];
                }
            }
        }

        public void SetSymbol(long lSymbol)
        {
            Symbol = lSymbol;
        }

        public ushort AskLevel(int level)
        {
            fixed (ushort* p = depthAskLevels)
            {
                return *(p + level);
            }
        }

        public ushort BidLevel(int level)
        {
            fixed (ushort* p = depthBidLevels)
            {
                return *(p + level);
            }
        }

        public void IncrementAskLevel(int level, ushort size)
        {
            fixed (ushort* ptr = depthAskLevels)
            {
                ptr[level] += size;
            }
        }

        public void IncrementBidLevel(int level, ushort size)
        {
            fixed (ushort* ptr = depthBidLevels)
            {
                ptr[level] += size;
            }
        }

        public void SetAskLevel(int level, ushort size)
        {
            fixed( ushort *ptr = depthAskLevels)
            {
                ptr[level] = (ushort)size;
            }
        }

        public void SetBidLevel(int level, ushort size)
        {
            fixed( ushort *ptr = depthBidLevels)
            {
                ptr[level] = (ushort)size;
            }
        }

        public int BidDepth
        {
            get
            {
                int total = 0;
                fixed (ushort* p = depthBidLevels)
                {
                    for (int i = 0; i < TickBinary.DomLevels; i++)
                    {
                        total += *(p + i);
                    }
                }
                return total;
            }
        }

        public int AskDepth
        {
            get
            {
                int total = 0;
                fixed (ushort* p = depthAskLevels)
                {
                    for (int i = 0; i < TickBinary.DomLevels; i++)
                    {
                        total += *(p + i);
                    }
                }
                return total;
            }
        }

        public int CompareTo(ref TickBinary other)
        {
            fixed( ushort *a1 = depthAskLevels)
            fixed( ushort *a2 = other.depthAskLevels)
            fixed( ushort *b1 = depthBidLevels)
            fixed( ushort *b2 = other.depthBidLevels)
            return contentMask == other.contentMask &&
                UtcTime == other.UtcTime &&
                Bid == other.Bid &&
                Ask == other.Ask &&
                Side == other.Side &&
                Price == other.Price &&
                Size == other.Size &&
                memcmp(a1, a2) &&
                memcmp(b1, b2) ? 0 :
                UtcTime > other.UtcTime ? 1 : -1;
        }

        public static bool memcmp(ushort* array1, ushort* array2)
        {
            for (int i = 0; i < TickBinary.DomLevels; i++)
            {
                if (*(array1 + i) != *(array2 + i)) return false;
            }
            return true;
        }

        public void CopyDepth(TickIO tick)
        {
            fixed( ushort* b = depthBidLevels)
            fixed( ushort* a = depthAskLevels)
            for (int i = 0; i < DomLevels; i++)
            {
                *(b + i) = (ushort)tick.BidLevel(i);
                *(a + i) = (ushort)tick.AskLevel(i);
            }
        }

        public unsafe void WriteBidSize(ref TickBinary lastBinary, byte field, int i, byte** ptr)
        {
            fixed (ushort* lp = lastBinary.depthBidLevels)
            fixed (ushort* p = depthBidLevels)
            {
                var diff = *(p + i) - *(lp + i);
                if (diff != 0)
                {
                    *(*ptr) = (byte)(field | i); (*ptr)++;
                    *(short*)(*ptr) = (short)diff; (*ptr) += sizeof(short);
                    *(lp + i) = *(p + i);
                }
            }
        }

        public unsafe void WriteAskSize(ref TickBinary lastBinary, byte field, int i, byte** ptr)
        {
            fixed (ushort* lp = lastBinary.depthAskLevels)
            fixed (ushort* p = depthAskLevels)
            {
                var diff = *(p + i) - *(lp + i);
                if (diff != 0)
                {
                    *(*ptr) = (byte)(field | i); (*ptr)++;
                    *(short*)(*ptr) = (short)diff; (*ptr) += sizeof(short);
                    *(lp + i) = *(p + i);
                }
            }
        }
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("UtcTime " + new TimeStamp(UtcTime) + ", ContentMask " + contentMask);
            sb.Append(", Bid " + Bid + ", Ask " + Ask);
            sb.Append(", Price " + Price + ", Size " + Size);
            sb.Append(", Strike " + Strike + ", UtcOptionExpiration " + UtcOptionExpiration);
            sb.Append(", BidSizes ");
            fixed (ushort* usptr = depthBidLevels)
                for (int i = 0; i < TickBinary.DomLevels; i++)
                {
                    var size = *(usptr + i);
                    if (i != 0) sb.Append(",");
                    sb.Append(size);
                }
            sb.Append(", AskSizes ");
            fixed (ushort* usptr = depthAskLevels)
                for (int i = 0; i < TickBinary.DomLevels; i++)
                {
                    var size = *(usptr + i);
                    if (i != 0) sb.Append(",");
                    sb.Append(size);
                }
            return sb.ToString();
        }
    }
}
