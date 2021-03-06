using System;
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Interceptors
{
    public class InternalOrders
    {
        private Strategy strategy;
        private TradeDirection direction;
        public InternalOrders(Strategy strategy, TradeDirection direction)
        {
            this.strategy = strategy;
            this.direction = direction;
        }

        private LogicalOrder buyMarket;
        public LogicalOrder BuyMarket
        {
            get
            {
                if (buyMarket == null)
                {
                    buyMarket = Factory.Engine.LogicalOrder(strategy.Data.SymbolInfo, strategy);
                    buyMarket.TradeDirection = direction;
                    buyMarket.Side = OrderSide.Buy;
                    buyMarket.Type = OrderType.Market;
                    strategy.AddOrder(buyMarket);
                }
                return buyMarket;
            }
        }
        private LogicalOrder sellMarket;
        public LogicalOrder SellMarket
        {
            get
            {
                if (sellMarket == null)
                {
                    sellMarket = Factory.Engine.LogicalOrder(strategy.Data.SymbolInfo, strategy);
                    sellMarket.TradeDirection = direction;
                    sellMarket.Side = OrderSide.Sell;
                    sellMarket.Type = OrderType.Market;
                    strategy.AddOrder(sellMarket);
                }
                return sellMarket;
            }
        }
        private LogicalOrder buyStop;
        public LogicalOrder BuyStop
        {
            get
            {
                if (buyStop == null)
                {
                    buyStop = Factory.Engine.LogicalOrder(strategy.Data.SymbolInfo, strategy);
                    buyStop.TradeDirection = direction;
                    buyStop.Side = OrderSide.Buy;
                    buyStop.Type = OrderType.Stop;
                    strategy.AddOrder(buyStop);
                }
                return buyStop;
            }
        }

        private LogicalOrder sellStop;
        public LogicalOrder SellStop
        {
            get
            {
                if (sellStop == null)
                {
                    sellStop = Factory.Engine.LogicalOrder(strategy.Data.SymbolInfo, strategy);
                    sellStop.TradeDirection = direction;
                    sellStop.Side = OrderSide.Sell;
                    sellStop.Type = OrderType.Stop;
                    strategy.AddOrder(sellStop);
                }
                return sellStop;
            }
        }
        private LogicalOrder buyLimit;
        public LogicalOrder BuyLimit
        {
            get
            {
                if (buyLimit == null)
                {
                    buyLimit = Factory.Engine.LogicalOrder(strategy.Data.SymbolInfo, strategy);
                    buyLimit.TradeDirection = direction;
                    buyLimit.Side = OrderSide.Buy;
                    buyLimit.Type = OrderType.Limit;
                    strategy.AddOrder(buyLimit);
                }
                return buyLimit;
            }
        }
        private LogicalOrder sellLimit;

        public LogicalOrder SellLimit
        {
            get
            {
                if (sellLimit == null)
                {
                    sellLimit = Factory.Engine.LogicalOrder(strategy.Data.SymbolInfo, strategy);
                    sellLimit.TradeDirection = direction;
                    sellLimit.Side = OrderSide.Sell;
                    sellLimit.Type = OrderType.Limit;
                    strategy.AddOrder(sellLimit);
                }
                return sellLimit;
            }
        }

        internal bool AreBuyOrdersActive
        {
            get { return (buyLimit != null && buyLimit.IsActive) ||
                (buyStop != null && buyStop.IsActive) ||
                (buyMarket != null && buyMarket.IsActive);
            }
        }

        internal bool AreSellOrdersActive
        {
            get { return (sellLimit != null && sellLimit.IsActive) ||
                (sellStop != null && sellStop.IsActive) || 
                (sellMarket != null && sellMarket.IsActive); }
        }

        internal bool AreBuyOrdersNextBar
        {
            get
            {
                return (buyLimit != null && buyLimit.IsNextBar) ||
                    (buyStop != null && buyStop.IsNextBar) ||
                    (buyMarket != null && buyMarket.IsNextBar);
            }
        }

        internal bool AreSellOrdersNextBar
        {
            get
            {
                return (sellLimit != null && sellLimit.IsNextBar) ||
                    (sellStop != null && sellStop.IsNextBar) ||
                    (sellMarket != null && sellMarket.IsNextBar);
            }
        }

        public void CancelOrders()
        {
            if( buyMarket != null) buyMarket.Status = OrderStatus.AutoCancel;
            if (sellMarket != null) sellMarket.Status = OrderStatus.AutoCancel;
            if (buyStop != null) buyStop.Status = OrderStatus.AutoCancel;
            if (sellStop != null) sellStop.Status = OrderStatus.AutoCancel;
            if (buyLimit != null) buyLimit.Status = OrderStatus.AutoCancel;
            if (sellLimit != null) sellLimit.Status = OrderStatus.AutoCancel;

        }
    }
}