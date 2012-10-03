using System;
using System.Drawing;
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    public class BaseSimpleStrategy : Strategy
    {
        protected IndicatorCommon bidLine;
        protected IndicatorCommon askLine;
        private IndicatorCommon position;
        private IndicatorCommon averagePrice;
        protected double ask;
        private double marketAsk;
        protected double bid;
        private double marketBid;
        protected double midPoint;
        protected double lastMarketBid;
        protected double lastMarketAsk;
        protected double increaseSpread;
        protected double lastMidPoint;
        private double breakEvenPrice;
        protected bool throttleIncreasing = false;
        protected bool isVisible = false;
        protected int sequentialIncreaseCount;
        protected double minimumTick;
        protected int lotSize = 1000;
        protected volatile StrategyState beforeWeekendState = StrategyState.Active;
        protected StrategyState state = StrategyState.Active;
        protected int positionPriorToWeekend = 0;
        bool isFirstTick = true;
        private int buySize = 1;
        private int sellSize = 1;
        protected InventoryGroupDefault inventory;

        public override void OnInitialize()
        {
            inventory = new InventoryGroupDefault(Data.SymbolInfo);
            inventory.MinimumLotSize = lotSize;
            Performance.Equity.GraphEquity = true; // Graphed by portfolio.
            Performance.GraphTrades = IsVisible;

            askLine = Formula.Indicator();
            askLine.Name = "Ask";
            askLine.Drawing.IsVisible = isVisible;

            bidLine = Formula.Indicator();
            bidLine.Name = "Bid";
            bidLine.Drawing.Color = Color.Blue;
            bidLine.Drawing.IsVisible = isVisible;

            averagePrice = Formula.Indicator();
            averagePrice.Name = "BE";
            averagePrice.Drawing.IsVisible = isVisible;
            averagePrice.Drawing.Color = Color.Black;

            position = Formula.Indicator();
            position.Name = "Position";
            position.Drawing.PaneType = PaneType.Secondary;
            position.Drawing.GroupName = "Position";
            position.Drawing.IsVisible = IsVisible;

            minimumTick = Data.SymbolInfo.MinimumTick;
        }

        protected void Reset()
        {
            if (Position.HasPosition)
            {
                BuySize = 0;
                SellSize = 0;
                Orders.Exit.ActiveNow.GoFlat();
            }
        }

        protected virtual void UpdateIndicators(Tick tick)
        {
            UpdateBreakEven((tick.Ask + tick.Bid) / 2);

            averagePrice[0] = breakEvenPrice;
            if (bidLine.Count > 0)
            {
                position[0] = Position.Current / lotSize;
            }
        }

        protected void CalcMarketPrices(Tick tick)
        {
            // Calculate market prics.
            marketAsk = Math.Max(tick.Ask, tick.Bid);
            marketBid = Math.Min(tick.Ask, tick.Bid);
            midPoint = (tick.Ask + tick.Bid) / 2;
        }

        protected virtual void SetFlatBidAsk()
        {
            var tick = Ticks[0];
            var midPoint = (tick.Bid + tick.Ask) / 2;
            var myAsk = midPoint + increaseSpread / 2;
            var myBid = midPoint - increaseSpread / 2;
            marketAsk = Math.Max(tick.Ask, tick.Bid);
            marketBid = Math.Min(tick.Ask, tick.Bid);
            ask = Math.Max(myAsk, marketAsk);
            bid = Math.Min(myBid, marketBid);
            bidLine[0] = bid;
            askLine[0] = ask;
        }

        protected void SetupBidAsk(double price)
        {
            UpdateBreakEven(price);
            lastMidPoint = midPoint;
            SetupBidAsk();
        }

        protected void UpdateBreakEven(double price)
        {
            if (Position.HasPosition)
            {
                breakEvenPrice = inventory.BreakEven;
            }
            else
            {
                breakEvenPrice = price;
            }
        }

        protected virtual void SetupBidAsk()
        {
            SetupAsk(midPoint);
            SetupBid(midPoint);
        }

        protected virtual void SetupAsk(double price)
        {
            var myAsk = midPoint;
            ask = Math.Max(myAsk, marketAsk);
            askLine[0] = ask;
        }

        protected virtual void SetupBid(double price)
        {
            var myBid = midPoint;
            bid = Math.Min(myBid, marketBid);
            bidLine[0] = bid;
        }

        //protected double CalcIndifferencePrice(TransactionPairBinary comboTrade)
        //{
        //    var size = Math.Abs(comboTrade.CurrentPosition);
        //    if (size == 0)
        //    {
        //        return midPoint;
        //    }
        //    var sign = -Math.Sign(comboTrade.CurrentPosition);
        //    var openPoints = comboTrade.AverageEntryPrice.ToLong() * size;
        //    var closedPoints = comboTrade.ClosedPoints.ToLong() * sign;
        //    var grossProfit = openPoints + closedPoints;
        //    var transaction = 0; // size * commission * sign;
        //    var expectedTransaction = 0; // size * commission * sign;
        //    var result = (grossProfit - transaction - expectedTransaction) / size;
        //    result = ((result + 5000) / 10000) * 10000;
        //    return result.ToDouble();
        //}

        protected void HandleWeekendRollover(Tick tick)
        {
            var time = tick.Time;
            var utcTime = tick.UtcTime;
            var dayOfWeek = time.GetDayOfWeek();
            switch (state)
            {
                default:
                    if (dayOfWeek == 5)
                    {
                        var hour = time.Hour;
                        var minute = time.Minute;
                        if (hour == 16 && minute > 30)
                        {
                            beforeWeekendState = state;
                            state = StrategyState.EndForWeek;
                            goto EndForWeek;
                        }
                    }
                    break;
                case StrategyState.EndForWeek:
                    EndForWeek:
                    if (dayOfWeek == 5)
                    {
                        if (Position.Current != 0)
                        {
                            positionPriorToWeekend = Position.Current;
                            if (positionPriorToWeekend > 0)
                            {
                                Orders.Change.ActiveNow.SellMarket(positionPriorToWeekend);
                            }
                            else if (positionPriorToWeekend < 0)
                            {
                                Orders.Change.ActiveNow.BuyMarket(Math.Abs(positionPriorToWeekend));
                            }
                        }
                        return;
                    }
                    if (Position.Current == positionPriorToWeekend)
                    {
                        state = beforeWeekendState;
                    }
                    else
                    {
                        if (positionPriorToWeekend > 0)
                        {
                            Orders.Change.ActiveNow.BuyMarket(positionPriorToWeekend);
                        }
                        if (positionPriorToWeekend < 0)
                        {
                            Orders.Change.ActiveNow.SellMarket(Math.Abs(positionPriorToWeekend));
                        }
                    }
                    break;
            }
        }

        protected void ProcessOrders(Tick tick)
        {
            if (Position.IsFlat)
            {
                OnProcessFlat(tick);
            }
            else if (Position.IsLong)
            {
                OnProcessLong(tick);
            }
            else if (Position.IsShort)
            {
                OnProcessShort(tick);
            }

        }
        private void OnProcessFlat(Tick tick)
        {
            if (isFirstTick)
            {
                isFirstTick = false;
                SetFlatBidAsk();
            }
            var comboTrades = Performance.ComboTrades;
            if (comboTrades.Count == 0 || comboTrades.Tail.Completed)
            {
                if (BuySize > 0)
                {
                    Orders.Enter.ActiveNow.BuyLimit(bid, BuySize * lotSize);
                }

                if (SellSize > 0)
                {
                    Orders.Enter.ActiveNow.SellLimit(ask, SellSize * lotSize);
                }
            }
            else
            {
                if (BuySize > 0)
                {
                    Orders.Change.ActiveNow.BuyLimit(bid, BuySize * lotSize);
                }
                if (SellSize > 0)
                {
                    Orders.Change.ActiveNow.SellLimit(ask, SellSize * lotSize);
                }
            }
        }

        private void OnProcessLong(Tick tick)
        {
            var lots = Position.Size / lotSize;
            if (BuySize > 0)
            {
                Orders.Change.ActiveNow.BuyLimit(bid, BuySize * lotSize);
            }
            if (SellSize > 0)
            {
                if (lots == SellSize)
                {
                    Orders.Exit.ActiveNow.SellLimit(ask);
                }
                else if( SellSize > lots)
                {
                    Orders.Reverse.ActiveNow.SellLimit(ask, lotSize);
                }
                else
                {
                    Orders.Change.ActiveNow.SellLimit(ask, SellSize * lotSize);
                }
            }
        }

        private void OnProcessShort(Tick tick)
        {
            var lots = Position.Size / lotSize;
            if (SellSize > 0)
            {
                Orders.Change.ActiveNow.SellLimit(ask, SellSize * lotSize);
            }
            if (BuySize > 0)
            {
                if (lots == BuySize)
                {
                    Orders.Exit.ActiveNow.BuyLimit(bid);
                }
                else if( BuySize > lots)
                {
                    Orders.Reverse.ActiveNow.BuyLimit(bid, lotSize);
                }
                else
                {
                    Orders.Change.ActiveNow.BuyLimit(bid, BuySize * lotSize);
                }
            }
        }
        public bool IsVisible
        {
            get { return isVisible; }
            set { isVisible = value; }
        }

        public int BuySize
        {
            get { return buySize; }
            set
            {
                if( value > 100)
                {
                    int x = 0;
                }
                buySize = value;
            }
        }

        public int SellSize
        {
            get { return sellSize; }
            set { sellSize = value; }
        }

        public double BreakEvenPrice
        {
            get { return breakEvenPrice; }
        }

        public double MarketAsk
        {
            get { return marketAsk; }
        }

        public double MarketBid
        {
            get { return marketBid; }
        }
    }
}