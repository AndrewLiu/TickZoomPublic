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
        private IndicatorCommon excursionLine;
        private double target;
        private int previousPosition;
        private int increaseCurveTicks = 160;


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
            bidSpread = offerSpread = startingSpread = 5 * minimumTick;
            BuySize = SellSize = baseLots;
            askLine.Drawing.IsVisible = true;
            bidLine.Drawing.IsVisible = true;

            //for( var x = 10; x < 100; x+=10)
            //{
            //    var y = Calc5LogisticsCurve(x);
            //    Log.InfoFormat("x,y = {0},{1}", x, y);
            //}

            //for (var highx = 10; highx < 100; highx += 10)
            //{
            //    Log.InfoFormat("High X = {0}", highx);
            //    var sum = 0D;
            //    for (var x = 0; x < highx; x ++)
            //    {
            //        var y = CalcDecreaseFactor(x, highx);
            //        Log.InfoFormat("x,y = {0},{1}", x, y);
            //        sum += y;
            //    }
            //    Log.InfoFormat("Sum Y = {0}", sum);
            //}
        }

        private void CreateIndicators()
        {
            targetLine = Formula.Indicator();
            targetLine.Name = "Target";
            targetLine.Drawing.IsVisible = true;
            targetLine.Drawing.Color = Color.Orange;

            excursionLine = Formula.Indicator();
            excursionLine.Name = "Excursion";
            excursionLine.Drawing.IsVisible = false;
            excursionLine.Drawing.Color = Color.Orange;
            excursionLine.Drawing.PaneType = PaneType.Secondary;
            excursionLine.Drawing.GroupName = "Excursion";

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

            //if (Position.HasPosition && inventory.AdverseExcursion / minimumTick > 500)
            //{
            //    Orders.Exit.ActiveNow.GoFlat();
            //}

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

        private double CalcProfitRetrace(int totalMinutes)
        {
            var profitDefaultRetrace = 0.20;
            var profitUpdateFrequency = 10;
            var profitUpdatePercent = 0.01;
            var profitMinimumPercent = -1.00;
            return Math.Max(profitMinimumPercent, profitDefaultRetrace - profitUpdatePercent * (totalMinutes / profitUpdateFrequency));
        }

        private int CalcMinutesInPosition()
        {
            var combo = Performance.ComboTrades.Tail;
            var tick = Ticks[0];
            var elapsed = tick.Time - combo.EntryTime;
            return elapsed.TotalMinutes;
        }

        private double CalcIncreaseSpread( int x)
        {
            var result = x*x*0.001*increaseCurveTicks + x + 0;
            return result;
        }

        private int retracePercent;
        private void SetBidOffer()
        {
            if( !Position.HasPosition)
            {
                return;
            }
            var minutes = CalcMinutesInPosition();
            var completeDistance = Math.Abs(inventory.MaxPrice - inventory.MinPrice);
            var targetPercent = CalcProfitRetrace(minutes);
            var targetPoints = completeDistance*targetPercent;

            target = Position.IsLong ? inventory.BreakEven + targetPoints : inventory.BreakEven - targetPoints;
            var lots = Position.Size/lotSize;
            highLots = lots > highLots ? lots : highLots;
            SellSize = 1;
            BuySize = 1;
            retracePercent = 0;
            bidSpread = startingSpread;
            offerSpread = startingSpread;
            if (Position.IsLong)
            {
                if (midPoint < target)
                {
                    //bidSpread = minimumTick*Math.Max(1, (50 - lots)/10);
                    bidSpread += (inventory.AdverseExcursion - inventory.Excursion)/ 10;
                    offerSpread = CalcDecreaseSpread(lots);
                }
                else
                {
                    SellSize = (int) Math.Max(1,Math.Min(lots,(inventory.Excursion / minimumTick)/10));
                }
                Orders.Exit.ActiveNow.SellLimit(target);
            }
            if (Position.IsShort)
            {
                if (midPoint > target)
                {
                    //offerSpread = minimumTick * Math.Max(1, (50 - lots)/10);
                    offerSpread += (inventory.AdverseExcursion - inventory.Excursion) / 10;
                    bidSpread = CalcDecreaseSpread(lots);
                }
                else
                {
                    BuySize = (int) Math.Max(1,Math.Min(lots, (inventory.Excursion / minimumTick) / 10));
                }
                Orders.Exit.ActiveNow.BuyLimit(target);
            }
            bid = midPoint - bidSpread;
            ask = midPoint + offerSpread;
            return;
        }

        private double Calc5LogisticsCurve(int x)
        {
            var a = -1.44476096837362;
            var b = 0.575858698581167;
            var y = Math.Pow(10, b + (Math.Log10(x)) * a);
            return y;
        }

        private double CalcDecreaseFactor( int x, int highx)
        {
            var a = Calc5LogisticsCurve(highx);
            var b = 1.5;
            var c = 0D;
            var d = 0.10;

            var y = a*Math.Pow(x, b) + c*x + d;
            return y;
        }


        private double CalcDecreaseSpread( int lots)
        {
            var retraceLots = lots;
            var factor = CalcDecreaseFactor(retraceLots, highLots);
            var increment = Math.Max(minimumTick, (inventory.AdverseExcursion/highLots) * factor);
            return increment;
        }

        private void UpdateIndicators()
        {
            bidLine[0] = bid.Round();
            askLine[0] = ask.Round();
            offerSpreadLine[0] = Math.Round(offerSpread, Data.SymbolInfo.MinimumTickPrecision);
            bidSpreadLine[0] = -Math.Round(bidSpread, Data.SymbolInfo.MinimumTickPrecision);
            excursionLine[0] = Math.Round(inventory.Excursion, Data.SymbolInfo.MinimumTickPrecision);
            if (Position.IsLong && midPoint < target)
            {
                targetLine[0] = Math.Round(target, Data.SymbolInfo.MinimumTickPrecision);
            }
            else if (Position.IsShort && midPoint > target)
            {
                targetLine[0] = Math.Round(target, Data.SymbolInfo.MinimumTickPrecision);
            }
            else
            {
                targetLine[0] = double.NaN;
            }
        }

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