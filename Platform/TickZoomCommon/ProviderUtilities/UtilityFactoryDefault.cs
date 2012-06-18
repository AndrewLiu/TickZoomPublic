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
using TickZoom.Api;
using TickZoom.Interceptors;

namespace TickZoom.Common
{
	[Diagram(AttributeExclude=true)]
	public class UtilityFactoryDefault : UtilityFactory
	{
        public PhysicalOrderStore PhyscalOrderStore(string name)
        {
            return new PhysicalOrderStoreDefault(name);
        }
        public PhysicalOrderCache PhyscalOrderCache(string name)
        {
            return new PhysicalOrderCacheDefault(name);
        }
        public PhysicalOrder PhysicalOrder(OrderState orderState, SymbolInfo symbol, PhysicalOrder origOrder)
        {
            return new PhysicalOrderDefault(orderState, symbol, origOrder);
        }
        public PhysicalOrder PhysicalOrder(OrderAction action, OrderState orderState, SymbolInfo symbol, OrderSide side, OrderType type, OrderFlags flags, double price, int remainingSize, int cumulativeSize, int completeSize, int logicalOrderId, long logicalSerialNumber, long brokerOrder, object tag, TimeStamp utcCreateTime)
        {
            return new PhysicalOrderDefault(action, orderState, symbol, side, type, flags, price, remainingSize, cumulativeSize, completeSize, logicalOrderId, logicalSerialNumber, brokerOrder, (string)tag, utcCreateTime);
        }

        public PhysicalOrder PhysicalOrder(OrderAction action, OrderState orderState, SymbolInfo symbol, OrderSide side, OrderType type, OrderFlags flags, double price, int remainingSize, int logicalOrderId, long logicalSerialNumber, long brokerOrder, object tag, TimeStamp utcCreateTime)
        {
            return new PhysicalOrderDefault(action, orderState, symbol, side, type, flags, price, remainingSize, logicalOrderId, logicalSerialNumber, brokerOrder, (string)tag, utcCreateTime);
        }

        public ProviderService CommandLineProcess()
        {
			return new CommandLineProcess();
		}
		public ProviderService WindowsService() {
			return new WindowsService();
		}

        public OrderAlgorithm OrderAlgorithm(string name, SymbolInfo symbol, PhysicalOrderHandler handler, PhysicalOrderHandler synthetics, LogicalOrderCache logicalCache, PhysicalOrderCache physicalQueue)
        {
            return new OrderAlgorithmDefault(name, symbol, handler, synthetics, logicalCache, physicalQueue);
		}
		public SymbolHandler SymbolHandler(string providerName, SymbolInfo symbol, Agent agent) {
			return new SymbolHandlerDefault(providerName, symbol,agent);
		}
		public VerifyFeed VerifyFeed(SymbolInfo symbol) {
			return (VerifyFeed) Factory.Parallel.SpawnPerformer(typeof(VerifyFeedDefault),symbol);
		}
		public FillSimulator FillSimulator(string name, SymbolInfo symbol, bool createExitStategyFills, bool createActualFills, TriggerController triggers) {
            return new FillSimulatorPhysical(name, symbol, createExitStategyFills, createActualFills, triggers);
		}
		public FillHandler FillHandler() {
			return new FillHandlerDefault();
		}
		public FillHandler FillHandler(StrategyInterface strategy) {
			return new FillHandlerDefault(strategy);
		}
		public BreakPointInterface BreakPoint() {
			return new BreakPoint();
		}
		[Diagram(AttributeExclude=true)]
		public PositionInterface Position(ModelInterface model) {
			return new PositionCommon(model);
		}

        public PhysicalFill PhysicalFill(SymbolInfo symbol, int size, double price, TimeStamp time, TimeStamp utcTime, long brokerOrder, bool isSimulated, int totalSize, int cumulativeSize, int remainingSize, bool isRealTime, bool isActual)
        {
            return new PhysicalFillDefault(symbol, size, price, time, utcTime, brokerOrder, isSimulated, totalSize, cumulativeSize, remainingSize, isRealTime, isActual);
		}
		
		public StrategyInterface Strategy() {
			return new Strategy();
		}

        #region UtilityFactory Members


        #endregion
    }
}
