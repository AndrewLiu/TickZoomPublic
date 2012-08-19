using System;
using System.Drawing;
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    public class Retrace2Strategy : BaseSimpleStrategy
    {
        private double startingSpread;
        private double bidSpread;
        private double offerSpread;
        private int maxLots;
        private int baseLots = 1;
        private int highLots;
        private double minimumTick;
        private IndicatorCommon offerSpreadLine;
        private IndicatorCommon bidSpreadLine;
        private IndicatorCommon targetLine;
        private double target;

        public RetraceDirection Direction
        {
            get { return direction; }
            set { direction = value; }
        }

        public override void OnInitialize()
        {
            lotSize = 10000;
            CreateIndicators();
            base.OnInitialize();
            minimumTick = Data.SymbolInfo.MinimumTick;
            bidSpread = offerSpread = startingSpread = 1 * minimumTick;
            BuySize = SellSize = baseLots;
            askLine.Drawing.IsVisible = true;
            bidLine.Drawing.IsVisible = true;

        }

        private void CreateIndicators()
        {
            targetLine = Formula.Indicator();
            targetLine.Name = "Target";
            targetLine.Drawing.IsVisible = true;
            targetLine.Drawing.Color = Color.Orange;

            offerSpreadLine = Formula.Indicator();
            offerSpreadLine.Name = "Offer Spread";
            offerSpreadLine.Drawing.IsVisible = true;
            offerSpreadLine.Drawing.Color = Color.Red;
            offerSpreadLine.Drawing.PaneType = PaneType.Secondary;
            offerSpreadLine.Drawing.GroupName = "Bid/Offer";

            bidSpreadLine = Formula.Indicator();
            bidSpreadLine.Name = "Bid Spread";
            bidSpreadLine.Drawing.IsVisible = true;
            bidSpreadLine.Drawing.Color = Color.Green;
            bidSpreadLine.Drawing.PaneType = PaneType.Secondary;
            bidSpreadLine.Drawing.GroupName = "Bid/Offer";
        }

        public override bool OnProcessTick(Tick tick)
        {
            if (!tick.IsQuote)
            {
                throw new InvalidOperationException("This strategy requires bid/ask quote data to operate.");
            }
            Orders.SetAutoCancel();

            CalcMarketPrices(tick);

            SetupBidAsk();

            inventory.UpdateBidAsk(MarketBid, MarketAsk);

            TryUpdatePegging();

            if (state.AnySet(StrategyState.ProcessOrders))
            {
                ProcessOrders(tick);
            }

            UpdateIndicators();
            return true;
        }

        protected  void TryUpdatePegging()
        {
            //bid = Math.Max(bid, Math.Min(midPoint,ask) - bidSpread);
            //ask = Math.Min(ask, Math.Max(midPoint,ask) + offerSpread);
        }

        protected override void SetFlatBidAsk()
        {
            bidSpread = offerSpread = startingSpread;
            bid = Math.Min(midPoint - bidSpread, MarketBid);
            ask = Math.Max(midPoint + offerSpread, MarketAsk);
            BuySize = 1;
            SellSize = 1;
            target = double.NaN;
        }

        protected override void SetupBidAsk()
        {

        }

        private double CalcProfitRetrace()
        {
            var combo = Performance.ComboTrades.Tail;
            var tick = Ticks[0];
            var elapsed = tick.Time - combo.EntryTime;
            var totalMinutes = elapsed.TotalMinutes;
            var profitDefaultRetrace = 1;
            var profitUpdateFrequency = 10;
            var profitUpdatePercent = 0.01;
            var profitMinimumPercent = 0.01;
            return Math.Max(profitMinimumPercent, profitDefaultRetrace - profitUpdatePercent * (totalMinutes / profitUpdateFrequency));
        }

        private void SetBidOffer()
        {
            var lots = Position.Size/lotSize;
            highLots = lots > highLots ? lots : highLots;
            SellSize = 1;
            BuySize = 1;
            var ae = inventory.AdverseExcursion;
            if (Position.IsLong)
            {
                bidSpread = minimumTick*lots;
                if( inventory.BreakEven == inventory.EntryPrice)
                {
                    target = inventory.BreakEven + 5 * minimumTick;
                    offerSpread = Math.Max(minimumTick, target - inventory.BreakEven);
                    SellSize = lots + 1;
                }
                else if( midPoint > target)
                {
                    target = (inventory.BreakEven - ae) + 2 * ae * CalcProfitRetrace();
                    offerSpread = minimumTick;
                    SellSize = lots + 1;
                }
                else if (midPoint > inventory.BreakEven)
                {
                    target = (inventory.BreakEven - ae) + 2 * ae * CalcProfitRetrace();
                    offerSpread = minimumTick;
                    SellSize = 1;
                }
                else
                {
                    target = (inventory.BreakEven - ae) + 2 * ae * CalcProfitRetrace();
                    offerSpread = 5 * Math.Max(minimumTick, inventory.Excursion / lots);
                }
            }
            if( Position.IsShort)
            {
                offerSpread = minimumTick*lots;
                if (inventory.BreakEven == inventory.EntryPrice)
                {
                    target = inventory.EntryPrice - 5 * minimumTick;
                    bidSpread = Math.Max(minimumTick, inventory.BreakEven - target);
                    BuySize = lots + 1;
                }
                else if (midPoint < target)
                {
                    target = (inventory.BreakEven + ae) - 2 * ae * CalcProfitRetrace();
                    bidSpread = minimumTick;
                    BuySize = lots + 1;
                }
                else if (midPoint < inventory.BreakEven)
                {
                    target = (inventory.BreakEven + ae) - 2 * ae * CalcProfitRetrace();
                    bidSpread = minimumTick;
                    BuySize = 1;
                }
                else
                {
                    target = (inventory.BreakEven + ae) - 2 * ae * CalcProfitRetrace();
                    bidSpread = Math.Max(minimumTick, inventory.Excursion / lots);
                }
            }
            bid = midPoint - bidSpread;
            ask = midPoint + offerSpread;
            return;
        }

        private void UpdateIndicators()
        {
            bidLine[0] = bid.Round();
            askLine[0] = ask.Round();
            offerSpreadLine[0] = offerSpread;
            bidSpreadLine[0] = - bidSpread;
            targetLine[0] = target;
        }

        private int previousPosition;
        private void ProcessChange(TransactionPairBinary comboTrade, LogicalFill fill)
        {
            var positionChange = fill.Position - previousPosition;
            previousPosition = fill.Position;
            inventory.Change(fill.Price, positionChange);
            var tick = Ticks[0];
            UpdateBreakEven((tick.Ask + tick.Bid) / 2);
            var lots = (Math.Abs(comboTrade.CurrentPosition) / lotSize);
            if( lots > maxLots)
            {
                maxLots = lots;
            }
            BuySize = SellSize = baseLots;
            offerSpread = bidSpread = startingSpread;
            SetBidOffer();
            UpdateIndicators(tick);
            UpdateIndicators();
        }

        private void ProcessExit()
        {
            inventory.Clear();
            previousPosition = 0;
            maxLots = 0;
            highLots = 0;
            bidSpread = startingSpread;
            offerSpread = startingSpread;
            BuySize = SellSize = baseLots;
        }

        public override void OnEnterTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            var tick = Ticks[0];
            ProcessChange(comboTrade, fill);
            UpdateIndicators(tick);
            UpdateIndicators();
        }

        public override void OnExitTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            ProcessExit();
            var tick = Ticks[0];
            UpdateBreakEven((tick.Ask + tick.Bid) / 2);
            UpdateIndicators(tick);
            UpdateIndicators();
            inventory.Retrace = 0.60D;
            SetFlatBidAsk();
        }

        public override void  OnChangeTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            ProcessChange(comboTrade, fill);
        }

        public override void OnReverseTrade(TransactionPairBinary comboTrade, TransactionPairBinary reversedCombo, LogicalFill fill, LogicalOrder order)
        {
            ProcessExit();
            ProcessChange(comboTrade, fill);
            var tick = Ticks[0];
            UpdateBreakEven((tick.Ask + tick.Bid) / 2);
            UpdateIndicators(tick);
            UpdateIndicators();
        }
    }
}