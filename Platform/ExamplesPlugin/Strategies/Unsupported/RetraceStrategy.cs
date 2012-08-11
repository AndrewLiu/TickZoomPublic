using System;
using TickZoom.Api;

namespace TickZoom.Examples
{
    public class RetraceStrategy : BaseSimpleStrategy
    {
        private double startingSpread;
        private double addInventorySpread;
        private double bidSpread;
        private double offerSpread;
        private int maxLots;
        private int baseLots = 1;

        public override void OnInitialize()
        {
            lotSize = 1000;
            base.OnInitialize();
            bidSpread = offerSpread = startingSpread = 3 * Data.SymbolInfo.MinimumTick;
            addInventorySpread = 3 * Data.SymbolInfo.MinimumTick;
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

            //HandleWeekendRollover(tick);

            if (state.AnySet(StrategyState.ProcessOrders))
            {
                ProcessOrders(tick);
            }

            UpdateIndicators();
            return true;
        }

        protected override void SetFlatBidAsk()
        {
            SetBidOffer();
        }

        protected override void SetupBidAsk()
        {

        }

        private void SetBidOffer()
        {
            bid = midPoint - bidSpread;
            ask = midPoint + offerSpread;
            var lots = Position.Size/lotSize;
            if( Position.IsLong)
            {
                bid = midPoint - bidSpread;
                ask = BreakEvenPrice + offerSpread;
                BuySize = Math.Max(lots / 5, 1);
                SellSize = lots + baseLots;
            }
            if( Position.IsShort)
            {
                ask = midPoint + offerSpread;
                bid = BreakEvenPrice - offerSpread;
                SellSize = Math.Max(lots / 5, 1);
                BuySize = lots + baseLots;
            }
        }

        private void UpdateIndicators()
        {
            bidLine[0] = bid.Round();
            askLine[0] = ask.Round();
        }

        private void ProcessChange(TransactionPairBinary comboTrade)
        {
            var tick = Ticks[0];
            UpdateBreakEven((tick.Ask + tick.Bid) / 2);
            var lots = (Math.Abs(comboTrade.CurrentPosition) / baseLots);
            if( lots > maxLots)
            {
                maxLots = lots;
                if( TryTimeStop())
                {
                    return;
                }
            }
            if( comboTrade.CurrentPosition > 0)
            {
                BuySize = SellSize = baseLots;
                if (comboTrade.ExitPrice < BreakEvenPrice)
                {
                    offerSpread = bidSpread = startingSpread;
                }
                else
                {
                    bidSpread = offerSpread = startingSpread;
                }
            }
            else if( comboTrade.CurrentPosition < 0)
            {
                BuySize = SellSize = baseLots;
                if (comboTrade.ExitPrice > BreakEvenPrice)
                {
                    bidSpread = offerSpread = startingSpread;
                }
                else
                {
                    bidSpread = offerSpread = startingSpread;
                }
            }
            SetBidOffer();
            UpdateIndicators(tick);
            UpdateIndicators();
        }

        private bool TryTimeStop()
        {
            var result = false;
            var elapsed = Position.HasPosition ? Performance.ComboTrades.Tail.ExitTime - Performance.ComboTrades.Tail.EntryTime : 0;
            if (elapsed.TotalSeconds >= 300)
            {
                Reset();
                result = true;
            }
            return result;
        }

        public override void OnEnterTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            var tick = Ticks[0];
            UpdateBreakEven((tick.Ask + tick.Bid)/2);
            ProcessChange(comboTrade);
            UpdateIndicators(tick);
            UpdateIndicators();
        }

        public override void OnExitTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            ProcessExit();
        }

        private void ProcessExit()
        {
            var tick = Ticks[0];
            UpdateBreakEven((tick.Ask + tick.Bid) / 2);
            maxLots = 0;
            bidSpread = startingSpread;
            offerSpread = startingSpread;
            BuySize = SellSize = baseLots;
            SetBidOffer();
            UpdateIndicators(tick);
            UpdateIndicators();
        }

        public override void  OnChangeTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            ProcessChange(comboTrade);
        }
    }
}