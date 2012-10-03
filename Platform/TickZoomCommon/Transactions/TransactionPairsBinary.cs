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
using TickZoom.Api;

namespace TickZoom.Transactions
{
	public class TransactionPairsBinary
	{
		string name = "TradeList";
		int count = 0;
		int capacity = 0;
		int pageSize = 0;
		int pageBits = 5;
		int pageMask = 0;
		TransactionPairsPages pages;
		TransactionPairsPage tail = null;
		TransactionPairsPage current = null;
		
		public BinaryStore TradeData {
			get { return pages.TradeData; }
		}
		
		private BinaryPage CreatePage() {
			return new TransactionPairsPage();
		}
		
		public TransactionPairsBinary(BinaryStore tradeData)
		{
			pages = new TransactionPairsPages(tradeData,CreatePage);
			pageSize = 1 << pageBits;
			pageMask = pageSize - 1;
		}

		private void AssureTail() {
			if( count > capacity) {
				if( tail!=null) {
					pages.WritePage(tail);
				}
				int pageNumber = HashPageNumber(count);
				tail = (TransactionPairsPage) pages.CreatePage(pageNumber,pageSize);
				current = tail;
				capacity += pageSize;
			}
		}
		
		private int HashPageNumber(int index) {
			return (index >> pageBits) + 1;
		}
		
		public void Add(TransactionPairBinary trade) {
			count ++;
			AssureTail();
			tail.Add(trade);
		}
		
		public TransactionPairsBinary GetCompletedList(TimeStamp time, double price, int bar) {
			if( count > 0) {
				TransactionPairBinary pair = this[count-1];
				if( !pair.Completed ) {
					pair.Update(price, time, bar);
					this[count-1] = pair;
				}
			}
			return this;
		}
		
		public TransactionPairBinary Tail {
			get { return this[count-1]; }
			set { this[count-1] = value; }
		}
		
		public int Count { 
			get { return count; }
		}
		
		public TransactionPairBinary this [int index] {
		    get { 
				index = GetPageIndex(index);
				return current[ index];
			}
		    set {
				index = GetPageIndex(index);
				current[index] = value;
			}
		}

		private int GetPageIndex( int index) {
			int pageNumber = HashPageNumber(index);
			int pageIndex = index & pageMask;
			if( current == null || current.PageNumber != pageNumber) {
				pages.TryRelease(current);
				current = (TransactionPairsPage) pages.GetPage(pageNumber);
			}
			return pageIndex;
		}
				
		public override string ToString()
		{
			if( name != "") {
				return base.ToString() + ": " + name;
			} else {
				return base.ToString();
			}
			
		}
		
		public string Name {
			get { return name; }
			set { name = value; }
		}
	}
}
