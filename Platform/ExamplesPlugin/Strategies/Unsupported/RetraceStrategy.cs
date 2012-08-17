using System;
using TickZoom.Api;

namespace TickZoom.Examples
{
    public enum RetraceDirection
    {
        LongOnly,
        ShortOnly,
    }
    public class RetraceStrategy : BaseSimpleStrategy
    {
        private double startingSpread;
        private double bidSpread;
        private double offerSpread;
        private double profitSpread;
        private int maxLots;
        private int baseLots = 1;
        private RetraceDirection direction = RetraceDirection.LongOnly;

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

            inventory.UpdateBidAsk(MarketBid, MarketAsk);

            SetBidOffer(true);

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
            switch( direction)
            {
                case RetraceDirection.LongOnly:
                    if( Position.IsFlat)
                    {
                        bid = Math.Max(bid, MarketBid);
                    }
                    break;
                case RetraceDirection.ShortOnly:
                    if( Position.IsFlat)
                    {
                        ask = Math.Min(ask, MarketAsk);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override void SetFlatBidAsk()
        {
            bid = Math.Min(midPoint - bidSpread, MarketBid);
            ask = Math.Max(midPoint + offerSpread, MarketAsk);
            switch( direction)
            {
                case RetraceDirection.LongOnly:
                    BuySize = 1;
                    SellSize = 0;
                    break;
                case RetraceDirection.ShortOnly:
                    BuySize = 0;
                    SellSize = 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
            var profitDefaultRetrace = 0.95;
            var profitUpdateFrequency = 10;
            var profitUpdatePercent = 0.01;
            var profitMinimumPercent = 0.05;
            return Math.Max(profitMinimumPercent, profitDefaultRetrace - profitUpdatePercent * (totalMinutes / profitUpdateFrequency));
        }

        private void SetBidOffer(bool onlyUpdateTarget)
        {
            if( !Position.HasPosition)
            {
                return;
            }
            var lots = Math.Abs(Position.Size / lotSize);
            if( lots == 2 && ! onlyUpdateTarget)
            {
                var x = 0;
            }
            inventory.Retrace = 0.55D;
            inventory.ProfitRetrace = CalcProfitRetrace();
            inventory.IncreaseSpread = inventory.DecreaseSpread = startingSpread;
            inventory.CalculateBidOffer(MarketBid,MarketAsk);
            inventory.CalcBreakEven();
            if (Position.IsLong)
            {
                if( onlyUpdateTarget)
                {
                    ask = inventory.ProfitTarget;
                    SellSize = lots; // +1;
                }
                else
                {
                    bid = Math.Min(inventory.Bid, midPoint);
                    BuySize = inventory.BidSize / lotSize;
                    ask = Math.Max(inventory.ProfitTarget, midPoint);
                    SellSize = lots; // +1;
                }
            }
            if (Position.IsShort)
            {
                if( onlyUpdateTarget)
                {
                    bid = inventory.ProfitTarget;
                    BuySize = lots; // +1;
                }
                else
                {
                    ask = Math.Max(inventory.Offer, midPoint);
                    SellSize = -inventory.OfferSize / lotSize;
                    bid = Math.Min(inventory.ProfitTarget, midPoint);
                    BuySize = lots; // +1;
                }
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
            SetBidOffer(false);
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