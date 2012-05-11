using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    public class MarketSimulatorStrategy : Strategy {
        private double tickOffset = 1;
        double multiplier = 1.0D;
        double minimumTick;
        int tradeSize;
		
        public MarketSimulatorStrategy() {
            Performance.GraphTrades = true;
            Performance.Equity.GraphEquity = true;
        }
		
        public override void OnInitialize()
        {
            tradeSize = Data.SymbolInfo.Level2LotSize;
            minimumTick = multiplier * Data.SymbolInfo.MinimumTick;
        }

        public override bool OnProcessTick(Tick tick)
        {
            var midPoint = 0D;
            if (tick.IsQuote)
            {
                midPoint = (tick.Ask + tick.Bid) / 2;
            }
            else if( tick.IsTrade)
            {
                midPoint = tick.Price;
            }
            var bid = midPoint - Data.SymbolInfo.MinimumTick * tickOffset;
            var ask = midPoint + Data.SymbolInfo.MinimumTick * tickOffset;
            if (Position.IsFlat) 
            {
                Orders.Change.ActiveNow.BuyLimit(bid, tradeSize);
                Orders.Change.ActiveNow.SellLimit(ask, tradeSize);
            }
            else if (Position.HasPosition)
            {
                Orders.Change.ActiveNow.BuyLimit(bid, tradeSize);
                Orders.Change.ActiveNow.SellLimit(ask, tradeSize);
            }
            return true;
        }

        public override bool OnWriteReport(string folder)
        {
            return false;
        }

        public double Multiplier {
            get { return multiplier; }
            set { multiplier = value; }
        }
    }
}