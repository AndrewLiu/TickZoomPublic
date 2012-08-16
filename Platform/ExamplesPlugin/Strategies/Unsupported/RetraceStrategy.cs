using System;
using TickZoom.Api;

namespace TickZoom.Examples
{
    public class RetraceStrategy : BaseSimpleStrategy
    {
        private double startingSpread;
        private double bidSpread;
        private double offerSpread;
        private double profitSpread;
        private int maxLots;
        private int baseLots = 1;

        public override void OnInitialize()
        {
            lotSize = 1000;
            base.OnInitialize();
            bidSpread = offerSpread = startingSpread = 3 * Data.SymbolInfo.MinimumTick;
            profitSpread = 3 * Data.SymbolInfo.MinimumTick;
            BuySize = SellSize = baseLots;
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

            TryUpdateTarget();

            if (state.AnySet(StrategyState.ProcessOrders))
            {
                ProcessOrders(tick);
            }

            UpdateIndicators();
            return true;
        }

        protected override void SetFlatBidAsk()
        {
            bid = Math.Min(midPoint - bidSpread, MarketBid);
            ask = Math.Max(midPoint + offerSpread, MarketAsk);
            BuySize = SellSize = 1;
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
            //return Math.Max(0.05, 0.90 - 0.01*(totalMinutes/100));
            var profitDefaultRetrace = 0.95;
            var profitUpdateFrequency = 10;
            var profitUpdatePercent = 0.01;
            var profitMinimumPercent = 0.05;
            return Math.Max(profitMinimumPercent, profitDefaultRetrace - profitUpdatePercent * (totalMinutes / profitUpdateFrequency));
            return totalMinutes > 30
                       ? Math.Max(profitMinimumPercent, profitDefaultRetrace - profitUpdatePercent*(totalMinutes/profitUpdateFrequency))
                       : profitDefaultRetrace;
        }

        private void TryUpdateTarget()
        {
            if (!Position.HasPosition)
            {
                return;
            }
            var lots = Math.Abs(Position.Size / lotSize);
            inventory.Retrace = 0.55D; // Math.Max(0.50, 0.60 - 0.01 * totalHours);
            inventory.ProfitRetrace = CalcProfitRetrace();
            inventory.IncreaseSpread = inventory.DecreaseSpread = startingSpread;
            inventory.CalculateBidOffer(MarketBid, MarketAsk);
            inventory.CalcBreakEven();
            if (Position.IsLong)
            {
                BuySize = inventory.BidSize / lotSize;
                SellSize = lots + 1;
                ask = inventory.ProfitTarget;
            }
            if (Position.IsShort)
            {
                SellSize = -inventory.OfferSize / lotSize;
                BuySize = lots + 1;
                bid = inventory.ProfitTarget;
            }
            return;
        }

        private void SetBidOffer()
        {
            if( !Position.HasPosition)
            {
                return;
            }
            var lots = Math.Abs(Position.Size / lotSize);
            inventory.Retrace = 0.55D; // Math.Max(0.50, 0.60 - 0.01 * totalHours);
            inventory.ProfitRetrace = CalcProfitRetrace();
            inventory.IncreaseSpread = inventory.DecreaseSpread = startingSpread;
            inventory.CalculateBidOffer(MarketBid,MarketAsk);
            inventory.CalcBreakEven();
            bid = Math.Min(inventory.Bid,MarketBid);
            ask = Math.Max(inventory.Offer,MarketAsk);
            if (Position.IsLong)
            {
                BuySize = inventory.BidSize / lotSize;
                SellSize = lots + 1;
                ask = Math.Max(inventory.ProfitTarget, MarketAsk);
            }
            if (Position.IsShort)
            {
                SellSize = - inventory.OfferSize / lotSize;
                BuySize = lots + 1;
                bid = Math.Min(inventory.ProfitTarget, MarketBid);
            }
            return;
        }

        private void UpdateIndicators()
        {
            bidLine[0] = bid.Round();
            askLine[0] = ask.Round();
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