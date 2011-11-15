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
        public PhysicalOrderCache PhyscalOrderCache()
        {
            return new PhysicalOrderCacheDefault();
        }
        public CreateOrChangeOrder PhysicalOrder(OrderState orderState, SymbolInfo symbol, CreateOrChangeOrder origOrder)
        {
            return new CreateOrChangeOrderDefault(orderState, symbol, origOrder);
        }
        public CreateOrChangeOrder PhysicalOrder(OrderAction action, OrderState orderState, SymbolInfo symbol, OrderSide side, OrderType type, OrderFlags flags, double price, int size, int logicalOrderId, long logicalSerialNumber, object brokerOrder, object tag, TimeStamp utcCreateTime)
        {
            return new CreateOrChangeOrderDefault(action, orderState, symbol, side, type, flags, price, size, logicalOrderId, logicalSerialNumber, (string)brokerOrder, (string)tag, utcCreateTime);
        }

        public ProviderService CommandLineProcess()
        {
			return new CommandLineProcess();
		}
		public ProviderService WindowsService() {
			return new WindowsService();
		}

		public OrderAlgorithm OrderAlgorithm(string name, SymbolInfo symbol, PhysicalOrderHandler handler, LogicalOrderCache logicalCache, PhysicalOrderCache physicalQueue) {
			return new OrderAlgorithmDefault(name,symbol,handler, logicalCache, physicalQueue);
		}
		public SymbolHandler SymbolHandler(SymbolInfo symbol, Receiver receiver) {
			return new SymbolHandlerDefault(symbol,receiver);
		}
		public VerifyFeed VerifyFeed(SymbolInfo symbol) {
			return new VerifyFeedDefault(symbol);
		}
		public FillSimulator FillSimulator(string name, SymbolInfo symbol, bool createSimulateFills) {
			return new FillSimulatorPhysical(name, symbol, createSimulateFills);
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

        public PhysicalFill PhysicalFill(int size, double price, TimeStamp time, TimeStamp utcTime, CreateOrChangeOrder order,
                                   bool isSimulated, int totalSize, int cumulativeSize, int remainingSize, bool isRealTime)
        {
			return new PhysicalFillDefault(size,price,time,utcTime,order,isSimulated,totalSize,cumulativeSize,remainingSize,isRealTime);
		}
		
		public StrategyInterface Strategy() {
			return new Strategy();
		}

        #region UtilityFactory Members


        #endregion
    }
}
