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
using System.Reflection;
using System.Threading;

using NUnit.Framework;
using TickZoom.Api;
using TickZoom.Common;
using TickZoom.Interceptors;

namespace Orders
{

	[TestFixture]
	public class OrderAlgorithmTest {
		SymbolInfo symbol = Factory.Symbol.LookupSymbol("CSCO");
		ActiveList<LogicalOrder> orders = new ActiveList<LogicalOrder>();
		TestOrderAlgorithm handler;
		Strategy strategy;
        private PhysicalOrderCache physicalCache;
		
		public OrderAlgorithmTest() {
		}
		
		[SetUp]
		public void Setup() {
            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            strategy = new Strategy();
            strategy.Context = new MockContext();
            physicalCache = Factory.Utility.PhyscalOrderCache();
            handler = new TestOrderAlgorithm(symbol, strategy, ProcessFill, physicalCache);
            orders.Clear();
		}
          
        private void ProcessFill( SymbolInfo symbol, LogicalFillBinary fill)
        {
            for( var current = orders.First; current != null; current = current.Next)
            {
                var order = current.Value;
                if( order.SerialNumber == fill.OrderSerialNumber)
                {
                    orders.Remove(current);
                }
            }
        }

        public int CreateLogicalEntry(OrderType type, double price, int size)
        {
            return CreateLogicalEntry(type, price, size, 1, 0).Id;
        }

        public LogicalOrder CreateLogicalEntryOrder(OrderType type, double price, int size)
        {
            return CreateLogicalEntry(type, price, size, 1, 0);
        }

        public LogicalOrder CreateLogicalEntry(OrderType type, double price, int size, int levels, int increment)
        {
			var logical = Factory.Engine.LogicalOrder(symbol,strategy);
			logical.Status = OrderStatus.Active;
			logical.TradeDirection = TradeDirection.Entry;
			logical.Type = type;
			logical.Price = price;
			logical.Position = size;
            if( levels > 1)
            {
                logical.SetMultiLevels((int) size, levels, increment);
            }
			orders.AddLast(logical);
			return logical;
		}

        public int CreateLogicalChange(OrderType type, double price, int size)
        {
            return CreateLogicalChange(type, price, size, 1, 0).Id;
        }

        public LogicalOrder CreateLogicalChange(OrderType type, double price, int size, int levels, int increment)
        {
            var logical = Factory.Engine.LogicalOrder(symbol, strategy);
            logical.Status = OrderStatus.Active;
            logical.TradeDirection = TradeDirection.Change;
            logical.Type = type;
            logical.Price = price;
            logical.Position = size;
            if (levels > 0)
            {
                logical.SetMultiLevels((int) size, levels, increment);
            }
            orders.AddLast(logical);
            return logical;
        }

        public int CreateLogicalExit(OrderType type, double price)
        {
			LogicalOrder logical = Factory.Engine.LogicalOrder(symbol,strategy);
			logical.Status = OrderStatus.Active;
			logical.TradeDirection = TradeDirection.Exit;
			logical.Type = type;
			logical.Price = price;
			orders.AddLast(logical);
			return logical.Id;
		}

        public int CreateLogicalReverse(OrderType type, double price, int size)
        {
            return CreateLogicalReverse(type, price, size, 1, 0).Id;
        }

        public LogicalOrder CreateLogicalReverse(OrderType type, double price, int size, int levels, int increment)
        {
            var logical = Factory.Engine.LogicalOrder(symbol, strategy);
            logical.Status = OrderStatus.Active;
            logical.TradeDirection = TradeDirection.Reverse;
            logical.Type = type;
            logical.Price = price;
            logical.Position = size;
            if (levels > 0)
            {
                logical.SetMultiLevels((int) size, levels, increment);
            }
            orders.AddLast(logical);
            return logical;
        }

        private void AssertOrder(OrderType type, int index, int size, double price)
        {
            var order = handler.Orders.CreatedOrders[index];
            Assert.AreEqual(type, order.Type);
            Assert.AreEqual(price, order.Price);
            Assert.AreEqual(size, order.Size);
            AssertBrokerOrder(order.BrokerOrder);
        }

        [Test]
        public void TestMultipleLevelChanges()
        {
            Assert.Ignore();
            handler.ClearPhysicalOrders();
            handler.TrySyncPosition();
            var sellLimit = CreateLogicalChange(OrderType.SellLimit, 157.12, 1000, 5, 5);
            var buyLimit = CreateLogicalChange(OrderType.BuyLimit, 154.12, 1000, 5, 5);

            var position = 0;
            handler.SetActualPosition(position);
            handler.SetDesiredPosition(position);
            handler.SetLogicalOrders(orders);
            handler.PerformCompare();

            Assert.AreEqual(0, handler.Orders.CanceledOrders.Count);
            Assert.AreEqual(0, handler.Orders.ChangedOrders.Count);
            Assert.AreEqual(10, handler.Orders.CreatedOrders.Count);

            handler.Orders.CreatedOrders.Sort( (a,b) => a.Price == b.Price ? 0 : a.Price < b.Price ? 1 : -1);

            var price = 157.12.ToLong();
            var minimumTick = symbol.MinimumTick.ToLong();
            var count = 0;
            for (var i = 4; i >= 0; i--)
            {
                var levelPrice = price + 5 * minimumTick * i;
                AssertOrder(OrderType.SellLimit, count, 1000, levelPrice.ToDouble());
                count++;
            }

            price = 154.12.ToLong();
            for (var i = 0; i < 5; i++)
            {
                var levelPrice = price - 5*minimumTick*i;
                AssertOrder(OrderType.BuyLimit, count, 1000, levelPrice.ToDouble());
                count++;
            }
        }

        [Test]
        public void TestMultipleLevelChangesRolled()
        {
            Assert.Ignore();
            handler.ClearPhysicalOrders();
            handler.TrySyncPosition();
            var sellLimit = CreateLogicalChange(OrderType.SellLimit, 157.12, 1000, 5, 5);
            var buyLimit = CreateLogicalChange(OrderType.BuyLimit, 154.12, 1000, 5, 5);

            var position = 0;
            handler.SetActualPosition(position);
            handler.SetDesiredPosition(position);
            handler.SetLogicalOrders(orders);
            handler.PerformCompare();

            Assert.AreEqual(0, handler.Orders.CanceledOrders.Count);
            Assert.AreEqual(0, handler.Orders.ChangedOrders.Count);
            Assert.AreEqual(10, handler.Orders.CreatedOrders.Count);

            handler.Orders.CreatedOrders.Sort((a, b) => a.Price == b.Price ? 0 : a.Price < b.Price ? 1 : -1);

            var price = 157.12.ToLong();
            var minimumTick = symbol.MinimumTick.ToLong();
            var count = 0;
            for (var i = 4; i >= 0; i--)
            {
                var levelPrice = price + 5 * minimumTick * i;
                AssertOrder(OrderType.SellLimit, count, 1000, levelPrice.ToDouble());
                count++;
            }

            price = 154.12.ToLong();
            for (var i = 0; i < 5; i++)
            {
                var levelPrice = price - 5 * minimumTick * i;
                AssertOrder(OrderType.BuyLimit, count, 1000, levelPrice.ToDouble());
                count++;
            }

            sellLimit.Price = 157.17;
            buyLimit.Price = 154.17;

            handler.Orders.ClearOutputOrders();
            handler.SetActualPosition(position);
            handler.SetDesiredPosition(position);
            handler.SetLogicalOrders(orders);
            handler.PerformCompare();

            Assert.AreEqual(0, handler.Orders.CanceledOrders.Count);
            Assert.AreEqual(2, handler.Orders.ChangedOrders.Count);
            Assert.AreEqual(0, handler.Orders.CreatedOrders.Count);
        }

        [Test]
		public void Test01FlatZeroOrders() {
			handler.ClearPhysicalOrders();
            handler.TrySyncPosition();
			
			int buyLimitId = CreateLogicalEntry(OrderType.BuyLimit,234.12,1000);
			int buyStopId = CreateLogicalEntry(OrderType.SellStop,154.12,1000);
			CreateLogicalExit(OrderType.SellLimit,334.12);
			CreateLogicalExit(OrderType.SellStop,134.12);
			CreateLogicalExit(OrderType.BuyLimit,124.12);
			CreateLogicalExit(OrderType.BuyStop,194.12);
			
			var position = 0;
			handler.SetActualPosition(position);
			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CreatedOrders.Count);
			
			CreateOrChangeOrder order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.BuyLimit,order.Type);
			Assert.AreEqual(234.12,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(buyLimitId,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);

			order = handler.Orders.CreatedOrders[1];
			Assert.AreEqual(OrderType.SellStop,order.Type);
			Assert.AreEqual(154.12,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(buyStopId,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
		}
		
		private void AssertBrokerOrder( string brokerOrder) {
			Assert.True( brokerOrder is string, "is string");
			var brokerOrderId = (string) brokerOrder;
			Assert.True( brokerOrderId.Contains("."));
		}
		
		[Test]
		public void Test02FlatTwoOrders() {
			handler.ClearPhysicalOrders();
			
			int buyLimitId = CreateLogicalEntry(OrderType.BuyLimit,234.12,1000);
			int sellStopId = CreateLogicalEntry(OrderType.SellStop,154.12,1000);
			CreateLogicalExit(OrderType.SellLimit,334.12);
			CreateLogicalExit(OrderType.SellStop,134.12);
			CreateLogicalExit(OrderType.BuyLimit,124.12);
			CreateLogicalExit(OrderType.BuyStop,194.12);
			
			string buyOrder = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,234.12,1000,buyLimitId,buyOrder);
			string sellOrder = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellStop,154.12,1000,sellStopId,sellOrder);
			
			var position = 0;
			handler.SetActualPosition(position);
			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
		}

        private void SetActualSize( int size)
        {
            if (size > 0)
            {
                CreateLogicalEntry(OrderType.BuyMarket, 234.12, size);
            }
            else
            {
                CreateLogicalEntry(OrderType.SellMarket, 234.12, Math.Abs(size));
            }
            handler.ProcessOrders(orders);
            orders.Clear();
            handler.ClearPhysicalOrders();
        }
            
		
		[Test]
		public void Test03LongEntryFilled() {
            var position = 1000;
            SetActualSize(position);
			
			CreateLogicalEntry(OrderType.BuyLimit,234.12,1000);
			int sellStopId = CreateLogicalEntry(OrderType.SellStop,154.12,1000);
			int sellLimitId = CreateLogicalExit(OrderType.SellLimit,334.12);
			int sellStop2Id = CreateLogicalExit(OrderType.SellStop,134.12);
			CreateLogicalExit(OrderType.BuyLimit,124.12);
			CreateLogicalExit(OrderType.BuyStop,194.12);
			
			string sellOrder = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellStop,154.12,1000,sellStopId,sellOrder);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CreatedOrders.Count);
			
			var brokerOrder = handler.Orders.CanceledOrders[0];
			Assert.AreEqual(sellOrder,brokerOrder);
			
			var order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.SellLimit,order.Type);
			Assert.AreEqual(334.12,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(sellLimitId,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
			
			order = handler.Orders.CreatedOrders[1];
			Assert.AreEqual(OrderType.SellStop,order.Type);
			Assert.AreEqual(134.12,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(sellStop2Id,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
		}
		
		[Test]
		public void Test04LongTwoOrders()
		{
		    SetActualSize(1000);
			
			CreateLogicalEntry(OrderType.BuyLimit,234.12,1000);
			CreateLogicalEntry(OrderType.SellStop,154.12,1000);
			int sellLimitId = CreateLogicalExit(OrderType.SellLimit,334.12);
			int sellStopId = CreateLogicalExit(OrderType.SellStop,134.12);
			CreateLogicalExit(OrderType.BuyLimit,124.12);
			CreateLogicalExit(OrderType.BuyStop,194.12);
            handler.SetLogicalOrders(orders);

		    string sellOrder1 = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellStop,134.12,1000,sellStopId,sellOrder1);
		    string sellOrder2 = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellLimit,334.12,1000,sellLimitId,sellOrder2);
			
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
		}
		
		[Test]
		public void Test04SyncLongTwoOrders() {
            SetActualSize(1000);
            //handler.SetActualPosition(0);
            
            int sellLimitId = CreateLogicalExit(OrderType.SellLimit,334.12);
			int sellStopId = CreateLogicalExit(OrderType.SellStop,134.12);
			CreateLogicalExit(OrderType.BuyLimit,124.12);
			CreateLogicalExit(OrderType.BuyStop,194.12);

			handler.SetDesiredPosition(1000);
			handler.ProcessOrders(orders);

            Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(3,handler.Orders.CreatedOrders.Count);

            //PhysicalOrder order = handler.Orders.CreatedOrders[0];
            //Assert.AreEqual(OrderType.BuyMarket,order.Type);
            //Assert.AreEqual(1000,order.Size);
            //Assert.AreEqual(0,order.LogicalOrderId);
            //AssertBrokerOrder(order.BrokerOrder);

            var order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.SellLimit,order.Type);
			Assert.AreEqual(334.12,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(sellLimitId,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
			
			order = handler.Orders.CreatedOrders[1];
			Assert.AreEqual(OrderType.SellStop,order.Type);
			Assert.AreEqual(134.12,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(sellStopId,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
		}
		
		[Test]
		public void Test05LongPartialEntry() {
			SetActualSize(500);

			// Position now long but an entry order is still working at
			// only part of the size.
			// So size is 500 but order is still 500 due to original order 1000;
			
			int buyLimitId = CreateLogicalEntry(OrderType.BuyLimit,234.12,1000);
			int sellStopId = CreateLogicalEntry(OrderType.SellStop,154.12,1000);
			int sellLimitId = CreateLogicalExit(OrderType.SellLimit,334.12);
			int sellStop2Id = CreateLogicalExit(OrderType.SellStop,134.12);
			CreateLogicalExit(OrderType.BuyLimit,124.12);
			CreateLogicalExit(OrderType.BuyStop,194.12);

		    string buyOrder = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,234.12,500,buyLimitId,buyOrder);
		    string sellOrder = "xyz";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellStop,154.12,1000,sellStopId,sellOrder);
			
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CreatedOrders.Count);
			
			var brokerOrder = handler.Orders.CanceledOrders[0];
			Assert.AreEqual(sellOrder,brokerOrder);
			
			var order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.SellLimit,order.Type);
			Assert.AreEqual(334.12,order.Price);
			Assert.AreEqual(500,order.Size);
			Assert.AreEqual(sellLimitId,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
			
			order = handler.Orders.CreatedOrders[1];
			Assert.AreEqual(OrderType.SellStop,order.Type);
			Assert.AreEqual(134.12,order.Price);
			Assert.AreEqual(500,order.Size);
			Assert.AreEqual(sellStop2Id,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
		}
		
		[Test]
		public void Test06LongPartialExit() {
			SetActualSize(500);
			
			int sellLimitId = CreateLogicalExit(OrderType.SellLimit,334.12);
			int sellStopId = CreateLogicalExit(OrderType.SellStop,134.12);
			CreateLogicalExit(OrderType.BuyLimit,124.12);
			CreateLogicalExit(OrderType.BuyStop,194.12);

		    string sellOrder1 = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellStop,134.12,1000,sellStopId,sellOrder1);
		    string sellOrder2 = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellLimit,334.12,500,sellLimitId,sellOrder2);
			
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(1,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			
			var change = handler.Orders.ChangedOrders[0];
			Assert.AreEqual(OrderType.SellStop,change.Order.Type);
			Assert.AreEqual(134.12,change.Order.Price);
			Assert.AreEqual(500,change.Order.Size);
			Assert.AreEqual(sellStopId,change.Order.LogicalOrderId);
			Assert.AreEqual(sellOrder1,change.OrigBrokerOrder);
		}
		
		[Test]
		public void Test07ShortEntryFilled() {
			SetActualSize(-1000);
			
			int buyLimitId = CreateLogicalEntry(OrderType.BuyLimit,234.12,1000);
			CreateLogicalEntry(OrderType.SellStop,154.12,1000);
			CreateLogicalExit(OrderType.SellLimit,334.12);
			CreateLogicalExit(OrderType.SellStop,134.12);
			int buyLimit2Id = CreateLogicalExit(OrderType.BuyLimit,124.12);
			int buyStopId = CreateLogicalExit(OrderType.BuyStop,194.12);
			
			string buyOrder = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,234.12,1000,buyLimitId,buyOrder);
			
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CreatedOrders.Count);
			
			var brokerOrder = handler.Orders.CanceledOrders[0];
			Assert.AreEqual(buyOrder,brokerOrder);
			
			var order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.BuyLimit,order.Type);
			Assert.AreEqual(124.12,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(buyLimit2Id,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
			
			order = handler.Orders.CreatedOrders[1];
			Assert.AreEqual(OrderType.BuyStop,order.Type);
			Assert.AreEqual(194.12,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(buyStopId,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
		}
		
		[Test]
		public void Test08ShortTwoOrders()
		{
		    SetActualSize(-1000);
			
			CreateLogicalEntry(OrderType.BuyLimit,234.12,1000);
			CreateLogicalEntry(OrderType.SellStop,154.12,1000);
			CreateLogicalExit(OrderType.SellLimit,334.12);
			CreateLogicalExit(OrderType.SellStop,134.12);
			int buyLimitId = CreateLogicalExit(OrderType.BuyLimit,124.12);
			int buyStopId = CreateLogicalExit(OrderType.BuyStop,194.12);
			
			string buyOrder1 = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,124.12,1000,buyLimitId,buyOrder1);
			string buyOrder2 = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyStop,194.12,1000,buyStopId,buyOrder2);
			
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
		}
		
		[Test]
		public void Test09ShortPartialEntry() {
			SetActualSize(-500);

			// Position now long but an entry order is still working at
			// only part of the size.
			// So size is 500 but order is still 500 due to original order 1000;
			
			int buyLimitId = CreateLogicalEntry(OrderType.BuyLimit,234.12,1000);
			int sellStopId = CreateLogicalEntry(OrderType.SellStop,154.12,1000);
			CreateLogicalExit(OrderType.SellLimit,334.12);
			CreateLogicalExit(OrderType.SellStop,134.12);
			int buyLimit2Id = CreateLogicalExit(OrderType.BuyLimit,124.12);
			int buyStopId = CreateLogicalExit(OrderType.BuyStop,194.12);
			
			string buyOrder = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,234.12,1000,buyLimitId,buyOrder);
			string sellOrder = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellStop,154.12,500,sellStopId,sellOrder);
			
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CreatedOrders.Count);
			
			var brokerOrder = handler.Orders.CanceledOrders[0];
			Assert.AreEqual(buyOrder,brokerOrder);
			
			var order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.BuyLimit,order.Type);
			Assert.AreEqual(124.12,order.Price);
			Assert.AreEqual(500,order.Size);
			Assert.AreEqual(buyLimit2Id,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
			
			order = handler.Orders.CreatedOrders[1];
			Assert.AreEqual(OrderType.BuyStop,order.Type);
			Assert.AreEqual(194.12,order.Price);
			Assert.AreEqual(500,order.Size);
			Assert.AreEqual(buyStopId,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
		}
		
		[Test]
		public void Test10ShortPartialExit()
		{
		    SetActualSize(-500);
			
			CreateLogicalExit(OrderType.SellLimit,334.12);
			CreateLogicalExit(OrderType.SellStop,134.12);
			int buyLimitId = CreateLogicalExit(OrderType.BuyLimit,124.12);
			int buyStopId = CreateLogicalExit(OrderType.BuyStop,194.12);
			
			string buyOrder1 = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,124.12,1000,buyLimitId,buyOrder1);
			string buyOrder2 = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyStop,194.12,1000,buyStopId,buyOrder2);
			
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(2,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			
			var change = handler.Orders.ChangedOrders[0];
			Assert.AreEqual(OrderType.BuyLimit,change.Order.Type);
			Assert.AreEqual(124.12,change.Order.Price);
			Assert.AreEqual(500,change.Order.Size);
			Assert.AreEqual(buyLimitId,change.Order.LogicalOrderId);
			Assert.AreEqual(buyOrder1,change.OrigBrokerOrder);
			
			change = handler.Orders.ChangedOrders[1];
			Assert.AreEqual(OrderType.BuyStop,change.Order.Type);
			Assert.AreEqual(194.12,change.Order.Price);
			Assert.AreEqual(500,change.Order.Size);
			Assert.AreEqual(buyStopId,change.Order.LogicalOrderId);
			Assert.AreEqual(buyOrder2,change.OrigBrokerOrder);
		}
		
		[Test]
		public void Test11FlatChangeSizes() {
			handler.ClearPhysicalOrders();
			
			int buyLimitId = CreateLogicalEntry(OrderType.BuyLimit,234.12,700);
			int sellStopId = CreateLogicalEntry(OrderType.SellStop,154.12,800);
			CreateLogicalExit(OrderType.SellLimit,334.12);
			CreateLogicalExit(OrderType.SellStop,134.12);
			CreateLogicalExit(OrderType.BuyLimit,124.12);
			CreateLogicalExit(OrderType.BuyStop,194.12);
			
			string buyOrder = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,234.12,1000,buyLimitId,buyOrder);
			string sellOrder = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellStop,154.12,1000,sellStopId,sellOrder);
			
			var position = 0; // Pretend we're flat.
			handler.SetActualPosition(position);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(2,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			
			var change = handler.Orders.ChangedOrders[0];
			Assert.AreEqual(OrderType.BuyLimit,change.Order.Type);
			Assert.AreEqual(234.12,change.Order.Price);
			Assert.AreEqual(700,change.Order.Size);
			Assert.AreEqual(buyLimitId,change.Order.LogicalOrderId);
			Assert.AreEqual(buyOrder,change.OrigBrokerOrder);
			
			change = handler.Orders.ChangedOrders[1];
			Assert.AreEqual(OrderType.SellStop,change.Order.Type);
			Assert.AreEqual(154.12,change.Order.Price);
			Assert.AreEqual(800,change.Order.Size);
			Assert.AreEqual(sellStopId,change.Order.LogicalOrderId);
			Assert.AreEqual(sellOrder,change.OrigBrokerOrder);
			
		}
		
		[Test]
		public void Test12FlatChangePrices() {
			handler.ClearPhysicalOrders();
			
			int buyLimitId = CreateLogicalEntry(OrderType.BuyLimit,244.12,1000);
			int sellStopId = CreateLogicalEntry(OrderType.SellStop,164.12,1000);
			CreateLogicalExit(OrderType.SellLimit,374.12);
			CreateLogicalExit(OrderType.SellStop,184.12);
			CreateLogicalExit(OrderType.BuyLimit,194.12);
			CreateLogicalExit(OrderType.BuyStop,104.12);
			
			string buyOrder = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,234.12,1000,buyLimitId,buyOrder);
			string sellOrder = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellStop,154.12,1000,sellStopId,sellOrder);
			
			var position = 0;
			handler.SetActualPosition(position);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(2,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			
			var change = handler.Orders.ChangedOrders[0];
			Assert.AreEqual(OrderType.BuyLimit,change.Order.Type);
			Assert.AreEqual(244.12,change.Order.Price);
			Assert.AreEqual(1000,change.Order.Size);
			Assert.AreEqual(buyLimitId,change.Order.LogicalOrderId);
			Assert.AreEqual(buyOrder,change.OrigBrokerOrder);
			
			change = handler.Orders.ChangedOrders[1];
			Assert.AreEqual(OrderType.SellStop,change.Order.Type);
			Assert.AreEqual(164.12,change.Order.Price);
			Assert.AreEqual(1000,change.Order.Size);
			Assert.AreEqual(sellStopId,change.Order.LogicalOrderId);
			Assert.AreEqual(sellOrder,change.OrigBrokerOrder);
			
		}
		
		[Test]
		public void Test13LongChangePrices() {
			handler.ClearPhysicalOrders();
            var position = 1000;
            SetActualSize(1000);
			
			CreateLogicalEntry(OrderType.BuyLimit,244.12,1000);
			CreateLogicalEntry(OrderType.SellStop,164.12,1000);
			int sellLimitId = CreateLogicalExit(OrderType.SellLimit,374.12);
			int sellStopId = CreateLogicalExit(OrderType.SellStop,184.12);
			CreateLogicalExit(OrderType.BuyLimit,194.12);
			CreateLogicalExit(OrderType.BuyStop,104.12);


            string sellOrder1 = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellStop,134.12,1000,sellStopId,sellOrder1);
			string sellOrder2 = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellLimit,334.12,1000,sellLimitId,sellOrder2);
			

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(2,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			
			var change = handler.Orders.ChangedOrders[0];
			Assert.AreEqual(OrderType.SellLimit,change.Order.Type);
			Assert.AreEqual(374.12,change.Order.Price);
			Assert.AreEqual(1000,change.Order.Size);
			Assert.AreEqual(sellLimitId,change.Order.LogicalOrderId);
			Assert.AreEqual(sellOrder2,change.OrigBrokerOrder);
			
			change = handler.Orders.ChangedOrders[1];
			Assert.AreEqual(OrderType.SellStop,change.Order.Type);
			Assert.AreEqual(184.12,change.Order.Price);
			Assert.AreEqual(1000,change.Order.Size);
			Assert.AreEqual(sellStopId,change.Order.LogicalOrderId);
			Assert.AreEqual(sellOrder1,change.OrigBrokerOrder);
			
		}
		
		[Test]
		public void Test13LongChangeSizes() {
			handler.ClearPhysicalOrders();
            var position = 1000;
            SetActualSize(position);
			
			CreateLogicalEntry(OrderType.BuyLimit,244.12,1000);
			CreateLogicalEntry(OrderType.SellStop,164.12,1000);
			int sellLimitId = CreateLogicalExit(OrderType.SellLimit,374.12);
			int sellStopId = CreateLogicalExit(OrderType.SellStop,184.12);
			CreateLogicalExit(OrderType.BuyLimit,194.12);
			CreateLogicalExit(OrderType.BuyStop,104.12);
			
			string sellOrder1 = "abc1";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellStop,184.12,700,sellStopId,sellOrder1);
			string sellOrder2 = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Sell,OrderType.SellLimit,374.12,800,sellLimitId,sellOrder2);
			
			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(2,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			
			var change = handler.Orders.ChangedOrders[0];
			Assert.AreEqual(OrderType.SellLimit,change.Order.Type);
			Assert.AreEqual(374.12,change.Order.Price);
			Assert.AreEqual(1000,change.Order.Size);
			Assert.AreEqual(sellLimitId,change.Order.LogicalOrderId);
			Assert.AreEqual(sellOrder2,change.OrigBrokerOrder);
			
			change = handler.Orders.ChangedOrders[1];
			Assert.AreEqual(OrderType.SellStop,change.Order.Type);
			Assert.AreEqual(184.12,change.Order.Price);
			Assert.AreEqual(1000,change.Order.Size);
			Assert.AreEqual(sellStopId,change.Order.LogicalOrderId);
			Assert.AreEqual(sellOrder1,change.OrigBrokerOrder);
			
		}
		
		[Test]
		public void Test14ShortToFlat() {
			handler.ClearPhysicalOrders();
			
			var position = -1000;
			handler.SetActualPosition(0); // Actual and desired differ!!!

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
            handler.TrySyncPosition();
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CreatedOrders.Count);
			
			CreateOrChangeOrder order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.SellMarket,order.Type);
			Assert.AreEqual(0,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(0,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
			
		}
		
		[Test]
		public void Test14AddToShort() {
			handler.ClearPhysicalOrders();
			
			var position = -4;
			handler.SetActualPosition(-2);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
            handler.TrySyncPosition();
           
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CreatedOrders.Count);
			
			CreateOrChangeOrder order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.SellMarket,order.Type);
			Assert.AreEqual(OrderSide.SellShort,order.Side);
			Assert.AreEqual(0,order.Price);
			Assert.AreEqual(2,order.Size);
			Assert.AreEqual(0,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
		}
		
		[Test]
		public void Test14ReverseFromLong() {
			handler.ClearPhysicalOrders();
			
			var position = -2;
			handler.SetActualPosition(2);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
            handler.TrySyncPosition();
            handler.FillCreatedOrders();

            var order = handler.Orders.CreatedOrders[0];
            Assert.AreEqual(0, handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CreatedOrders.Count);
			
			Assert.AreEqual(OrderType.SellMarket,order.Type);
			Assert.AreEqual(OrderSide.Sell,order.Side);
			Assert.AreEqual(0,order.Price);
			Assert.AreEqual(2,order.Size);
			Assert.AreEqual(0,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);

            order = handler.Orders.CreatedOrders[0];
            Assert.AreEqual(OrderType.SellMarket, order.Type);
            Assert.AreEqual(OrderSide.Sell, order.Side);
            Assert.AreEqual(0, order.Price);
            Assert.AreEqual(2, order.Size);
            Assert.AreEqual(0, order.LogicalOrderId);

            AssertBrokerOrder(order.BrokerOrder);
            handler.ClearPhysicalOrders();
			handler.SetActualPosition( 0);
            handler.TrySyncPosition();
            handler.FillCreatedOrders();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CreatedOrders.Count);
			
			order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.SellMarket,order.Type);
			Assert.AreEqual(OrderSide.SellShort,order.Side);
			Assert.AreEqual(0,order.Price);
			Assert.AreEqual(2,order.Size);
			Assert.AreEqual(0,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
		}
		
		[Test]
		public void Test14ReduceFromLong() {
			handler.ClearPhysicalOrders();
			
			var desiredPosition = 1;
			handler.SetActualPosition(2);

			handler.SetDesiredPosition(desiredPosition);
			handler.SetLogicalOrders(null);
            handler.TrySyncPosition();
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CreatedOrders.Count);
			
			CreateOrChangeOrder order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.SellMarket,order.Type);
			Assert.AreEqual(OrderSide.Sell,order.Side);
			Assert.AreEqual(0,order.Price);
			Assert.AreEqual(1,order.Size);
			Assert.AreEqual(0,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
			
		}
		
		[Test]
		public void Test15LongToFlat() {
			handler.ClearPhysicalOrders();
			
			var position = 1000;
			handler.SetActualPosition(0); // Actual and desired differ!!!

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
            handler.TrySyncPosition();
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CreatedOrders.Count);
			
			CreateOrChangeOrder order = handler.Orders.CreatedOrders[0];
			Assert.AreEqual(OrderType.BuyMarket,order.Type);
			Assert.AreEqual(0,order.Price);
			Assert.AreEqual(1000,order.Size);
			Assert.AreEqual(0,order.LogicalOrderId);
			AssertBrokerOrder(order.BrokerOrder);
			
		}
		
		[Test]
		public void Test16ActiveSellMarket() {
			handler.ClearPhysicalOrders();
			
			var position = -10;
			handler.SetActualPosition(0); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellMarket,134.12,10,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
		}
		
		[Test]
		public void Test17ActiveBuyMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 10;
			handler.SetActualPosition(-5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
		}
		
		[Test]
		public void Test18ActiveExtraBuyMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 10;
			handler.SetActualPosition(-5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test19ActiveExtraSellMarket() {
			handler.ClearPhysicalOrders();
			
			var position = -10;
			handler.SetActualPosition(5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellMarket,134.12,15,0,sellOrder1);
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test20ActiveUnneededSellMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 0;
			handler.SetActualPosition(0); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test21ActiveUnneededBuyMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 0;
			handler.SetActualPosition(0); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test22ActiveWrongSideSellMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 10;
			handler.SetActualPosition(-5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test23ActiveWrongSideBuyMarket() {
			handler.ClearPhysicalOrders();
			
			var position = -10;
			handler.SetActualPosition(5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test24ActiveBuyLimit() {
			handler.ClearPhysicalOrders();
			
			var position = 10;
			handler.SetActualPosition(-5);
			
			string sellOrder1 = "abc";
            var buyLimit = CreateLogicalEntryOrder(OrderType.BuyLimit, 134.12, 15);
            handler.Orders.AddPhysicalOrder(OrderState.Active, OrderSide.Buy, OrderType.BuyLimit, 134.12, 15, buyLimit, sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
            handler.TrySyncPosition();
            handler.FillCreatedOrders();
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
			
		}
		
		[Test]
		public void Test25ActiveSellLimit() {
			handler.ClearPhysicalOrders();
			
			var position = -10;
			handler.SetActualPosition(5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
            var sellLimit = CreateLogicalEntryOrder(OrderType.SellLimit, 134.12, 15);
            handler.Orders.AddPhysicalOrder(OrderState.Active, OrderSide.SellShort, OrderType.SellLimit, 134.12, 15, sellLimit, sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
            handler.TrySyncPosition();
            handler.FillCreatedOrders();
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
			
		}
		
		[Test]
		public void Test26ActiveBuyAndSellLimit() {
			handler.ClearPhysicalOrders();
			
			CreateLogicalEntry(OrderType.BuyMarket,0,2);
			
			var position = 0;
			handler.SetActualPosition(0);
			
			string sellOrder1 = "abc1";
			string buyOrder1 = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,15.12,3,0,buyOrder1);
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellLimit,34.12,3,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test27PendingSellMarket() {
			handler.ClearPhysicalOrders();
			
			var position = -10;
			handler.SetActualPosition(0); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.SellShort,OrderType.SellMarket,134.12,10,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
		}
		
		[Test]
		public void Test28PendingBuyMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 10;
			handler.SetActualPosition(-5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
		}
		
		[Test]
		public void Test29PendingExtraBuyMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 10;
			handler.SetActualPosition(-5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test30PendingExtraSellMarket() {
			handler.ClearPhysicalOrders();
			
			var position = -10;
			handler.SetActualPosition(5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.SellShort,OrderType.SellMarket,134.12,15,0,sellOrder1);
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.SellShort,OrderType.SellMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test31PendingUnneededSellMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 0;
			handler.SetActualPosition(0); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.SellShort,OrderType.SellMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test32PendingUnneededBuyMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 0;
			handler.SetActualPosition(0); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test33PendingWrongSideSellMarket() {
			handler.ClearPhysicalOrders();
			
			var position = 10;
			handler.SetActualPosition(-5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.SellShort,OrderType.SellMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test34PendingWrongSideBuyMarket() {
			handler.ClearPhysicalOrders();
			
			var position = -10;
			handler.SetActualPosition(5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.Buy,OrderType.BuyMarket,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test35PendingBuyLimit() {
			handler.ClearPhysicalOrders();
			
			var position = 10;
			handler.SetActualPosition(-5);
			
			string sellOrder1 = "abc";
			handler.Orders.AddPhysicalOrder(OrderState.Pending,OrderSide.Buy,OrderType.BuyLimit,134.12,15,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(0,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test36PendingSellLimit() {
			handler.ClearPhysicalOrders();
			
			var position = -10;
			handler.SetActualPosition(5); // Actual and desired differ!!!
			
			string sellOrder1 = "abc";
            var sellLimit = CreateLogicalEntryOrder(OrderType.SellLimit, 134.12, 15);
            handler.Orders.AddPhysicalOrder(OrderState.Pending, OrderSide.SellShort, OrderType.SellLimit, 134.12, 15, sellLimit, sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(null);
            handler.TrySyncPosition();
            handler.FillCreatedOrders();
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CanceledOrders.Count);
		}
		
		[Test]
		public void Test37PendingBuyAndSellLimit() {
			handler.ClearPhysicalOrders();
			
			CreateLogicalEntry(OrderType.BuyMarket,0,2);
			
			var position = 0;
			handler.SetActualPosition(0); // Actual and desired differ!!!
			
			string sellOrder1 = "abc1";
			string buyOrder1 = "abc2";
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.Buy,OrderType.BuyLimit,15.12,3,0,buyOrder1);
			handler.Orders.AddPhysicalOrder(OrderState.Active,OrderSide.SellShort,OrderType.SellLimit,34.12,3,0,sellOrder1);

			handler.SetDesiredPosition(position);
			handler.SetLogicalOrders(orders);
			handler.PerformCompare();
			
			Assert.AreEqual(0,handler.Orders.ChangedOrders.Count);
			Assert.AreEqual(1,handler.Orders.CreatedOrders.Count);
			Assert.AreEqual(2,handler.Orders.CanceledOrders.Count);
		}
		
		public class Change {
			public CreateOrChangeOrder Order;
			public string OrigBrokerOrder;
			public Change( CreateOrChangeOrder order, string origBrokerOrder) {
				this.Order = order;
				this.OrigBrokerOrder = origBrokerOrder;
			}
		}
		
		public class MockPhysicalOrderHandler : PhysicalOrderHandler {
			public List<object> CanceledOrders = new List<object>();
			public List<Change> ChangedOrders = new List<Change>();
            public List<CreateOrChangeOrder> CreatedOrders = new List<CreateOrChangeOrder>();
			public List<CreateOrChangeOrder> activeOrders = new List<CreateOrChangeOrder>();
            private PhysicalOrderConfirm confirmOrders;
		    private PhysicalOrderCache physicalCache;
			
			private SymbolInfo symbol;
			public MockPhysicalOrderHandler(SymbolInfo symbol, PhysicalOrderCache physicalOrderCache) {
				this.symbol = symbol;
			    this.physicalCache = physicalOrderCache;
			}

            public bool HasBrokerOrder( CreateOrChangeOrder order)
            {
                return false;
            }

            public int ProcessOrders()
            {
                return 1;
            }

			public bool OnCancelBrokerOrder(CreateOrChangeOrder order)
			{
				CanceledOrders.Add(order.OriginalOrder.BrokerOrder);
				RemoveByBrokerOrder(order.OriginalOrder.BrokerOrder);
				if( confirmOrders != null)
				{
				    order.OriginalOrder.ReplacedBy = order;
					confirmOrders.ConfirmCancel(order.OriginalOrder,true);
				}
                return true;
            }
			private void RemoveByBrokerOrder(string brokerOrder) {
				for( int i=0; i<activeOrders.Count; i++) {
					var order = activeOrders[i];
					if( order.BrokerOrder == brokerOrder) {
						activeOrders.Remove(order);
					}
				}
			}
			public bool OnChangeBrokerOrder(CreateOrChangeOrder order)
			{
				ChangedOrders.Add(new Change( order, order.OriginalOrder.BrokerOrder));
                RemoveByBrokerOrder(order.OriginalOrder.BrokerOrder);
				activeOrders.Add( order);
				if( confirmOrders != null) {
					confirmOrders.ConfirmChange(order,true);
				}
			    return true;
			}
			public bool OnCreateBrokerOrder(CreateOrChangeOrder order)
			{
				CreatedOrders.Add(order);
				activeOrders.Add(order);
				if( confirmOrders != null) {
					confirmOrders.ConfirmCreate(order,true);
				}
                return true;
            }
			public void ClearPhysicalOrders()
			{
				CanceledOrders.Clear();
				ChangedOrders.Clear();
				CreatedOrders.Clear();
				activeOrders.Clear();
			}
            public void ClearOutputOrders()
            {
                CanceledOrders.Clear();
                ChangedOrders.Clear();
                CreatedOrders.Clear();
            }
            public void AddPhysicalOrder(CreateOrChangeOrder order)
			{
				activeOrders.Add(order);
			}
			
			public void AddPhysicalOrder(OrderState orderState, OrderSide side, OrderType type, double price, int size, int logicalOrderId, string brokerOrder)
			{
                var order = Factory.Utility.PhysicalOrder(OrderAction.Create, orderState, symbol, side, type, OrderFlags.None, price,
                                                          size, logicalOrderId, 0, brokerOrder, null,
                                                          TimeStamp.UtcNow);
                activeOrders.Add(order);
                confirmOrders.ConfirmCreate(order, false);
			}

            public void AddPhysicalOrder(OrderState orderState, OrderSide side, OrderType type, double price, int size, LogicalOrder logicalOrder, string brokerOrder)
            {
                var order = Factory.Utility.PhysicalOrder(OrderAction.Create, orderState, symbol, side, type, OrderFlags.None, price, size, logicalOrder.Id, logicalOrder.SerialNumber, brokerOrder, null, TimeStamp.UtcNow);
                activeOrders.Add(order);
                confirmOrders.ConfirmCreate(order, false);
            }

            public Iterable<CreateOrChangeOrder> GetActiveOrders(SymbolInfo symbol)
			{
				var result = new ActiveList<CreateOrChangeOrder>();
				foreach( var order in activeOrders) {
					result.AddLast( order);
				}
				return result;
			}

            public PhysicalOrderConfirm ConfirmOrders
            {
				get { return confirmOrders; }
				set { confirmOrders = value; }
			}
		}

		public class TestOrderAlgorithm
		{
			private OrderAlgorithm orderAlgorithm;
			private MockPhysicalOrderHandler orders;
		    private Iterable<StrategyPosition> strategyPositions = new ActiveList<StrategyPosition>();
			private SymbolInfo symbol;
			private Strategy strategy;
		    private PhysicalOrderCache physicalCache;
			public TestOrderAlgorithm(SymbolInfo symbol, Strategy strategy, Action<SymbolInfo, LogicalFillBinary> onProcessFill, PhysicalOrderCache physicalOrderCache) {
                this.symbol = symbol;
                this.strategy = strategy;
			    this.physicalCache = physicalOrderCache;
                orders = new MockPhysicalOrderHandler(symbol, physicalCache);
                var logicalCache = Factory.Engine.LogicalOrderCache(symbol, false);
                orderAlgorithm = Factory.Utility.OrderAlgorithm("test", symbol, orders, logicalCache, physicalCache);
                orderAlgorithm.OnProcessFill = onProcessFill;
                orderAlgorithm.TrySyncPosition(new ActiveList<StrategyPosition>());
                orders.ConfirmOrders = orderAlgorithm;
			}
			public void ClearPhysicalOrders() {
				orders.ClearPhysicalOrders();
			}
            public void ClearSyncPosition()
            {
                orderAlgorithm.IsPositionSynced = false;
            }
			public void SetActualPosition( int position) {
				orderAlgorithm.SetActualPosition(position);
			}
			public void SetDesiredPosition( int position) {
				strategy.Position.Change(position,100.00,TimeStamp.UtcNow);
				orderAlgorithm.SetDesiredPosition(position);
			}

            public void ProcessOrders(Iterable<LogicalOrder> orders)
            {
                SetLogicalOrders(orders);
                orderAlgorithm.ProcessOrders();
                FillCreatedOrders();
            }

            public void SetLogicalOrders(Iterable<LogicalOrder> logicalOrders)
            {
                orderAlgorithm.SetStrategyPositions(strategyPositions);
                orderAlgorithm.SetLogicalOrders(logicalOrders);
            }

            public void TrySyncPosition()
            {
                ClearSyncPosition();
                orderAlgorithm.TrySyncPosition(strategyPositions);
                FillMarketOrders();
                orderAlgorithm.TrySyncPosition(strategyPositions);
                FillMarketOrders();
            }


			public void PerformCompare()
			{
                orderAlgorithm.ProcessOrders();
			}
			
			public double ActualPosition {
				get { return orderAlgorithm.ActualPosition; }
			}
			
			public void ProcessFill(PhysicalFill fill)
			{
				orderAlgorithm.ProcessFill(fill);
			}
			
			public MockPhysicalOrderHandler Orders {
				get { return orders; }
			}

            public void FillMarketOrders()
            {
                var ordersCopy = orders.activeOrders.ToArray();
                for (int i = 0; i < ordersCopy.Length; i++)
                {
                    var physical = ordersCopy[i];
                    if (physical.Type == OrderType.BuyMarket || physical.Type == OrderType.SellMarket)
                    {
                        FillOrder(physical);
                    }
                }
            }

		    public void FillCreatedOrders()
            {
                var ordersCopy = orders.activeOrders.ToArray();
                for (int i = 0; i < ordersCopy.Length; i++)
                {
                    var physical = ordersCopy[i];
                    FillOrder(physical);
                }
            }

            private void FillOrder(CreateOrChangeOrder physical)
            {
                var price = physical.Price == 0 ? 1234.12 : physical.Price;
                var size = physical.Type == OrderType.BuyLimit || physical.Type == OrderType.BuyStop ||
                           physical.Type == OrderType.BuyMarket
                               ? physical.Size
                               : -physical.Size;
                var fill = Factory.Utility.PhysicalFill(size, physical.Price, TimeStamp.UtcNow, TimeStamp.UtcNow, physical, false, size, size, 0, true, true);
                orders.activeOrders.Remove(physical);
                orderAlgorithm.ProcessFill(fill);
            }
		}
	}
}