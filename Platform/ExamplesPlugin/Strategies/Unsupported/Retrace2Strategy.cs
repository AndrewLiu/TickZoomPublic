using System;
using System.Collections.Generic;
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
        private TimeStamp suspendTradingTime = TimeStamp.MaxValue;
        private TimeStamp continueTradingTime = TimeStamp.MinValue;

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
            startingSpread = 10 * minimumTick;
            bidSpread = offerSpread = startingSpread/2;
            BuySize = SellSize = 0;
            askLine.Drawing.IsVisible = true;
            bidLine.Drawing.IsVisible = true;

            var newsLines = NewsTimes.Split('\n');
            NewsTimeStamps = new List<TimeStamp>();
            for( var i = 0; i< newsLines.Length; i++)
            {
                var newsLine = newsLines[i].Trim();
                if (string.IsNullOrEmpty(newsLine)) continue;
                try
                {
                    var timeStamp = new TimeStamp(newsLine);
                    NewsTimeStamps.Add(timeStamp);
                }
                catch(Exception ex)
                {
                    var x = 0;
                }
            }
            //NewsTimeStamps.Clear();
            GetNextNewsTime();
        }

        private List<TimeStamp> NewsTimeStamps;

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

            if( state.AnySet( StrategyState.ProcessOrders) && tick.UtcTime > suspendTradingTime)
            {
                continueTradingTime = suspendTradingTime;
                continueTradingTime.AddMinutes(5);
                Orders.Exit.ActiveNow.GoFlat();
                BuySize = SellSize = 0;
                suspendTradingTime = TimeStamp.MaxValue;
                state ^= StrategyState.Active;
            }

            if (!state.AnySet(StrategyState.ProcessOrders))
            {
                if (tick.UtcTime > continueTradingTime)
                {
                    GetNextNewsTime();
                    continueTradingTime = TimeStamp.MinValue;
                    state |= StrategyState.Active;
                    SetFlatBidAsk();
                }
                else
                {
                    Orders.Exit.ActiveNow.GoFlat();
                }
            }

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

        private void GetNextNewsTime()
        {
            if (NewsTimeStamps.Count > 0)
            {
                suspendTradingTime = NewsTimeStamps[0];
                suspendTradingTime.AddSeconds(-10);
                NewsTimeStamps.RemoveAt(0);
            }
            else
            {
                suspendTradingTime = TimeStamp.MaxValue;
            }
        }

        protected  void TryUpdatePegging()
        {
            //bid = Math.Max(bid, Math.Min(midPoint,ask) - bidSpread);
            //ask = Math.Min(ask, Math.Max(midPoint,ask) + offerSpread);
        }

        protected override void SetFlatBidAsk()
        {
            if (!state.AnySet(StrategyState.ProcessOrders)) return;
            bidSpread = offerSpread = startingSpread / 2;
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
            if( !Position.HasPosition || !state.AnySet(StrategyState.ProcessOrders))
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
            offerSpreadLine[0] = BuySize > 0 ? Math.Round(offerSpread, Data.SymbolInfo.MinimumTickPrecision) : double.NaN;
            bidSpreadLine[0] = SellSize > 0 ? -Math.Round(bidSpread, Data.SymbolInfo.MinimumTickPrecision) : double.NaN;
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
            bidSpread = offerSpread = startingSpread/2;
            BuySize = SellSize = 0;
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

        private string NewsTimes = @"
08-13-2012 11:50pm
08-14-2012 12:30pm
08-14-2012 2:00pm
08-15-2012 12:30pm
08-15-2012 1:00pm
08-15-2012 1:15pm
08-15-2012 2:00pm
08-15-2012 2:30pm
08-16-2012 12:30pm
08-16-2012 2:00pm
08-16-2012 2:30pm
08-17-2012 1:55pm
08-17-2012 2:00pm
08-21-2012 12:00pm
08-21-2012 11:50pm
08-22-2012 1:20pm
08-22-2012 2:30pm
08-22-2012 6:00pm
08-23-2012 12:30pm
08-23-2012 1:00pm
08-23-2012 2:00pm
08-23-2012 2:00pm
08-23-2012 2:30pm
08-23-2012 11:50pm
08-24-2012 7:45am
08-24-2012 12:30pm
08-24-2012 12:30pm
";
    }

 }