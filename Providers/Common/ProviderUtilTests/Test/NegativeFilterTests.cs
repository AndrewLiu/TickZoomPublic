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
using System.IO;
using System.Threading;

using NUnit.Framework;
using TickZoom.Api;

namespace TickZoom.Test
{

	public abstract class NegativeFilterTests : BaseProviderTests {
        int secondsDelay = 60;
        int sizeIncrease = 2000;
	    int overSizeIncrease = 10000;
		
		[Test]
		[ExpectedException( typeof(Exception), ExpectedMessage="was greater than MaxPositionSize of ", MatchType=MessageMatch.Contains)]
		public void TestFIXPositionFilterBuy() {
	  		int expectedPosition = 0;
			using( VerifyFeed verify = Factory.Utility.VerifyFeed(symbol))
			using( var provider = ProviderFactory()) {
				provider.SendEvent(new EventItem(verify.Task,(int)EventType.Connect));
                provider.SendEvent(new EventItem(verify.Task, symbol, (int)EventType.StartSymbol, new StartSymbolDetail(TimeStamp.MinValue)));
				VerifyConnected(verify);				
				ClearOrders(0);
				ClearPosition(provider,verify,secondsDelay);
	  			long count = verify.Verify(2,assertTick,secondsDelay);
	  			Assert.GreaterOrEqual(count,2,"tick count");
	  			int actualPosition = 0;
	  			int strategyPosition = 0;
                var strategy = Factory.Utility.Strategy();
                strategy.Context = new MockContext();
                while (true)
                {
					ClearOrders(0);
					CreateChange(strategy,OrderType.BuyMarket,0.0,(int)sizeIncrease,strategyPosition);
					SendOrders(provider,verify,actualPosition,30);
		  			expectedPosition += sizeIncrease;
		  			actualPosition = verify.VerifyPosition(sizeIncrease,secondsDelay);
		  			Assert.AreEqual(expectedPosition, actualPosition, "Increasing position.");
	  			}
			}
		}		
		
#if !OTHERS
		
		[Test]
		[ExpectedException( typeof(Exception), ExpectedMessage="was greater than MaxPositionSize of ", MatchType=MessageMatch.Contains)]
		public void TestFIXPositionFilterSell() {
	  		var expectedPosition = 0;
			using( VerifyFeed verify = Factory.Utility.VerifyFeed(symbol))
			using( Provider provider = ProviderFactory()) {
                provider.SendEvent(new EventItem(verify.Task, (int)EventType.Connect));
                provider.SendEvent(new EventItem(verify.Task, symbol, (int)EventType.StartSymbol, new StartSymbolDetail(TimeStamp.MinValue)));
				VerifyConnected(verify);				
				ClearOrders(0);
				ClearPosition(provider,verify,secondsDelay);
	  			long count = verify.Verify(2,assertTick,secondsDelay);
	  			Assert.GreaterOrEqual(count,2,"tick count");
	  			var actualPosition = 0;
                var strategy = Factory.Utility.Strategy();
                strategy.Context = new MockContext();
                while (true)
                {
					ClearOrders(0);
					CreateChange(strategy,OrderType.SellMarket,0.0,sizeIncrease,actualPosition);
					SendOrders(provider,verify,actualPosition,30);
		  			expectedPosition-=sizeIncrease;
		  			actualPosition = verify.VerifyPosition(sizeIncrease,secondsDelay);
		  			Assert.AreEqual(expectedPosition, actualPosition, "Increasing position.");
	  			}
			}
		}		
		
		[Test]
		[ExpectedException( typeof(Exception), ExpectedMessage="was greater than MaxOrderSize of ", MatchType=MessageMatch.Contains)]
		public void TestFIXPretradeOrderFilterBuy() {
			var expectedPosition = 0;
	  		int secondsDelay = 3;
			using( VerifyFeed verify = Factory.Utility.VerifyFeed(symbol))
			using( Provider provider = ProviderFactory()) {
                provider.SendEvent(new EventItem(verify.Task, (int)EventType.Connect));
                provider.SendEvent(new EventItem(verify.Task, symbol, (int)EventType.StartSymbol, new StartSymbolDetail(TimeStamp.MinValue)));
				VerifyConnected(verify);				
				ClearOrders(0);
				ClearPosition(provider,verify,secondsDelay);
	  			long count = verify.Verify(2,assertTick,secondsDelay);
	  			Assert.GreaterOrEqual(count,2,"tick count");
                expectedPosition = overSizeIncrease;
                var strategy = Factory.Utility.Strategy();
                strategy.Context = new MockContext();
                CreateChange(strategy, OrderType.BuyMarket, 0.0, (int)expectedPosition, 0);
	  			SendOrders(provider,verify,0,30);
	  			var position = verify.VerifyPosition(expectedPosition,secondsDelay);
	  			Assert.AreEqual(expectedPosition, position, "Increasing position.");
	  			Thread.Sleep(2000);
			}
		}		
	
		[Test]
		[ExpectedException( typeof(Exception), ExpectedMessage="was greater than MaxOrderSize of ", MatchType=MessageMatch.Contains)]
		public void TestFIXPretradeOrderFilterSell() {
			var expectedPosition = 0;
			using( VerifyFeed verify = Factory.Utility.VerifyFeed(symbol))
			using( Provider provider = ProviderFactory()) {
                provider.SendEvent(new EventItem(verify.Task, (int)EventType.Connect));
                provider.SendEvent(new EventItem(verify.Task, symbol, (int)EventType.StartSymbol, new StartSymbolDetail(TimeStamp.MinValue)));
				VerifyConnected(verify);				
				ClearOrders(0);
				ClearPosition(provider,verify,secondsDelay);
	  			long count = verify.Verify(2,assertTick,secondsDelay);
	  			Assert.GreaterOrEqual(count,2,"tick count");
	  			expectedPosition = -overSizeIncrease;
	  			var strategy = Factory.Utility.Strategy();
                strategy.Context = new MockContext();
                CreateChange(strategy, OrderType.SellMarket, 0.0, (int)Math.Abs(expectedPosition), 0);
	  			SendOrders(provider,verify,0,30);
	  			var position = verify.VerifyPosition(expectedPosition,secondsDelay);
	  			Assert.AreEqual(expectedPosition, position, "Increasing position.");
	  			Thread.Sleep(2000);
			}
		}		
#endif
	}
}
