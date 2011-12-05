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
	public abstract class BaseProviderTests {
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(BaseProviderTests));
		private readonly bool debug = log.IsDebugEnabled;		
		public abstract Provider ProviderFactory();
		protected ActiveList<LogicalOrder> orders = new ActiveList<LogicalOrder>();
		protected SymbolInfo symbol;
		protected Action<TickIO, TickIO, long> assertTick;
		private string providerAssembly;
		private string assemblyName;
		private int lotSize = 1;
		private TickSync tickSync;
		protected StrategyInterface strategy;
		
		public BaseProviderTests() {
			string providerAssembly = Factory.Settings["ProviderAssembly"];
			if( string.IsNullOrEmpty(providerAssembly)) {
				SetProviderAssembly( "TickZoomCombinedMock");
			} else {
				SetProviderAssembly( providerAssembly);
			}
		}
			
		[TestFixtureSetUp]
		public virtual void Init()
		{
			this.strategy = Factory.Utility.Strategy();
			this.strategy.Context = new MockContext();
			string appData = Factory.Settings["AppDataFolder"];
//			File.Delete( Factory.SysLog.LogFolder + @"\" + assemblyName+"Tests.log");
//			File.Delete( Factory.SysLog.LogFolder + @"\" + assemblyName+".log");			
		}
		
		public void SetProviderAssembly( string providerAssembly) {
			this.providerAssembly = providerAssembly;	
			var strings = providerAssembly.Split( new char[] { '/', '\\' } );
			assemblyName = strings[0];
		}
		
		public virtual Provider CreateProvider(bool inProcessFlag) {
			Provider provider;
			if( inProcessFlag) {
				provider = ProviderFactory();
			} else {
				provider = Factory.Provider.ProviderProcess("127.0.0.1",6492,providerAssembly);
			}
			return provider;
		}
		
		[SetUp]
		public virtual void Setup() {
			
		}
		
		[TearDown]
		public virtual void TearDown() {
			long start = Factory.TickCount;
			long elapsed = 0;
			var maxTasks = 1; // For Selector task
			if( Factory.Parallel.Tasks.Length > maxTasks) {
				log.Warn("Found " + Factory.Parallel.Tasks.Length + " Parallel tasks still running...");
			}
			while( elapsed < 10000 && Factory.Parallel.Tasks.Length > maxTasks) {
				Thread.Sleep(1000);
				elapsed = Factory.TickCount - start;
			}
			if( Factory.Parallel.Tasks.Length > maxTasks) {
				log.Error("These tasks still running after " + elapsed + "ms.");
				log.Error(Factory.Parallel.GetStats());
			}
			Assert.LessOrEqual(Factory.Parallel.Tasks.Length,maxTasks,"running tasks");
		}
		
		public void SetSymbol( string symbolString) {
			symbol = Factory.Symbol.LookupSymbol(symbolString);
			if( SyncTicks.Enabled) {
				tickSync = SyncTicks.GetTickSync(symbol.BinaryIdentifier);
			}
		}
		
		public enum TickTest {
			TimeAndSales,
			Level1
		}
		
		public void VerifyConnected(VerifyFeed verify) {
	  		var expectedBrokerState = BrokerState.Connected;
	  		var expectedSymbolState = SymbolState.RealTime;
	  		var actualState = verify.VerifyState(expectedBrokerState, expectedSymbolState,600);
	  		Assert.IsTrue(actualState,"Expected " + expectedBrokerState + " and " + expectedSymbolState);
		}
	
		public void ClearOrders(int temp) {
			orders.Clear();
		}
		
		public void ClearPosition(Provider provider, VerifyFeed verify, int secondsDelay) {
			var expectedPosition = 0;
  			var actualPosition = verify.VerifyPosition(expectedPosition,secondsDelay, () => {
  			                                                                                    ClearPositionInternal(provider,verify,expectedPosition);
  			});
  			Assert.AreEqual(expectedPosition, actualPosition, "Starting position.");
		}
		
		private void ClearPositionInternal(Provider provider, VerifyFeed verify, int expectedPosition) {
  			if( SyncTicks.Enabled) tickSync.AddPositionChange("Test");
		    var strategyPositions = new ActiveList<StrategyPosition>();
  			provider.SendEvent(verify,symbol,(int)EventType.PositionChange,new PositionChangeDetail(symbol,expectedPosition,orders,strategyPositions,TimeStamp.UtcNow.Internal,1L));
		}

        public LogicalOrder CreateChange(StrategyInterface strategy, OrderType orderType, double price, int position, int strategyPosition)
        {
            return CreateOrder(strategy, TradeDirection.Change, orderType, price, position, strategyPosition);
        }

        public LogicalOrder CreateEntry(StrategyInterface strategy, OrderType orderType, double price, int position, int strategyPosition)
        {
			return CreateOrder(strategy, TradeDirection.Entry, orderType,price,position,strategyPosition);
		}
		public LogicalOrder CreateExit( StrategyInterface strategy, OrderType orderType, double price, int strategyPosition) {
			return CreateOrder(strategy, TradeDirection.Exit,orderType,price,0,strategyPosition);
		}
		
		public LogicalOrder CreateOrder( StrategyInterface strategy, TradeDirection tradeDirection, OrderType orderType, double price, int position, int strategyPosition) {
  			LogicalOrder order = Factory.Engine.LogicalOrder(symbol,strategy);
  			order.StrategyId = 1;
  			order.StrategyPosition = strategyPosition;
  			order.TradeDirection = tradeDirection;
  			order.Type = orderType;
  			order.Price = price;
  			order.Position = position * lotSize;
  			order.Status = OrderStatus.Active;
            strategy.AddOrder(order);
            orders.AddLast(order);
            strategy.Position.Change(strategyPosition, 100.00, TimeStamp.UtcNow);
  			return order;
		}
		
		public void AssertLevel1( TickIO tick, TickIO lastTick, long symbol) {
	        	Assert.IsTrue(tick.IsQuote || tick.IsTrade);
	        	if( tick.IsQuote) {
	        	Assert.Greater(tick.Bid,0);
	        	Assert.Greater(tick.Ask,0);
	        	}
	        	if( tick.IsTrade) {
	        	Assert.Greater(tick.Price,0);
	    	    	Assert.Greater(tick.Size,0);
	        	}
	    		Assert.AreEqual(symbol,tick.lSymbol);
		}
		
		public void AssertTimeAndSales( TickIO tick, TickIO lastTick, long symbol) {
	        	Assert.IsFalse(tick.IsQuote);
	        	if( tick.IsQuote) {
	        	Assert.Greater(tick.Bid,0);
	        	Assert.Greater(tick.BidLevel(0),0);
	        	Assert.Greater(tick.Ask,0);
	        	Assert.Greater(tick.AskLevel(0),0);
	        	}
	        	Assert.IsTrue(tick.IsTrade);
	        	if( tick.IsTrade) {
	        	Assert.Greater(tick.Price,0);
	    	    	Assert.Greater(tick.Size,0);
	        	}
	    		Assert.AreEqual(symbol,tick.lSymbol);
		}
		
		public void SetTickTest( TickTest test) {
			switch( test) {
				case TickTest.Level1:
					assertTick = AssertLevel1;
					break;
				case TickTest.TimeAndSales:
					assertTick = AssertTimeAndSales;
					break;
			}
		}
		
//		public LogicalOrder CreateEntry(OrderType type, double price, int size, int strategyId) {
//			var logical = Factory.Engine.LogicalOrder(symbol,strategy);
//			logical.StrategyId = strategyId;
//			logical.StrategyPosition = 0;
//	  		logical.Status = OrderStatus.Active;
//			logical.TradeDirection = TradeDirection.Entry;
//			logical.Type = type;
//			logical.Price = price;
//			logical.Positions = size * lotSize;
//			orders.AddLast(logical);
//			return logical;
//		}
		
//		public LogicalOrder CreateLogicalEntry(OrderType type, double price, int size) {
//			LogicalOrder logical = Factory.Engine.LogicalOrder(symbol,strategy);
//	  			logical.Status = OrderStatus.Active;
//			logical.TradeDirection = TradeDirection.Entry;
//			logical.Type = type;
//			logical.Price = price;
//			logical.Positions = size * lotSize;
//			orders.AddLast(logical);
//			return logical;
//		}
		
//		public LogicalOrder CreateLogicalExit(OrderType type, double price) {
//			LogicalOrder logical = Factory.Engine.LogicalOrder(symbol,strategy);
//	  		logical.Status = OrderStatus.Active;
//			logical.TradeDirection = TradeDirection.Exit;
//			logical.Type = type;
//			logical.Price = price;
//			orders.AddLast(logical);
//			return logical;
//		}
		
		public void SendOrders(Provider provider, VerifyFeed verify, int desiredPosition, int secondsDelay) {
  			if( SyncTicks.Enabled) tickSync.AddPositionChange("Test");
            var strategyPositions = new ActiveList<StrategyPosition>();
            provider.SendEvent(verify, symbol, (int)EventType.PositionChange, new PositionChangeDetail(symbol, desiredPosition, orders, strategyPositions, TimeStamp.UtcNow.Internal,1L));
		}
		
		public string ProviderAssembly {
			get { return providerAssembly; }
		}
		
		public string AssemblyName {
			get { return assemblyName; }
		}
		
		public int LotSize {
			get { return lotSize; }
			set { lotSize = value; }
		}
	}	
	public class MockContext : Context {
		int modelId = 0;
		int logicalOrderId = 0;
        static readonly long startingLogicalSerialNumber = 1000000000;
		long logicalOrderSerialNumber = startingLogicalSerialNumber;
		public BinaryStore TradeData {
			get { throw new NotImplementedException(); }
		}
		public void AddOrder(LogicalOrder order)
		{ throw new NotImplementedException(); }
		public int IncrementOrderId() {
			return Interlocked.Increment(ref logicalOrderId);
		}
		public long IncrementOrderSerialNumber(long symbolBinary)
		{
		    return Interlocked.Increment(ref logicalOrderSerialNumber) + startingLogicalSerialNumber*symbolBinary;
		}
		public int IncrementModelId() {
			return Interlocked.Increment(ref modelId);
		}
	}
}