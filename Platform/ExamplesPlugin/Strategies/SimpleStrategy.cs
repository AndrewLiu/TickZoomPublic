﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using TickZoom.Api;
using TickZoom.Common;
using TickZoom.Interceptors;

namespace TickZoom.Examples
{
    public enum Direction
    {
        Long,
        Short,
        Both
    }

    public class SimpleStrategy: Strategy
    {
        IndicatorCommon bidLine;
        IndicatorCommon askLine;
        IndicatorCommon position;
        IndicatorCommon averagePrice;
        bool isFirstTick = true;
        double minimumTick;
        double spread;
        int lotSize;
        double ask;
        double bid;
        private int addDelaySeconds = 15;
        private int thresholdLots = 5;
        private Direction direction = Direction.Both;
        private Action<SimpleStrategy> onDirectionChange;
        private bool isVisible = false;

        public SimpleStrategy()
        {
            
        }

        public Action<SimpleStrategy> OnDirectionChange
        {
            get { return onDirectionChange; }
            set { onDirectionChange = value; }
        }

        public Direction Direction
        {
            get { return direction; }
            set { direction = value; }
        }

        public bool IsVisible
        {
            get { return isVisible; }
            set { isVisible = value; }
        }

        public override void OnConfigure()
        {
            base.OnConfigure();
        }

        public override void OnInitialize()
        {
            Performance.Equity.GraphEquity = false; // Graphed by portfolio.
            Performance.GraphTrades = isVisible;
            minimumTick = Data.SymbolInfo.MinimumTick;
            lotSize = 1000;
            spread = 15*minimumTick;

            askLine = Formula.Indicator();
            askLine.Name = "Ask";
            askLine.Drawing.IsVisible = isVisible;

            bidLine = Formula.Indicator();
            bidLine.Name = "Bid";
            bidLine.Drawing.IsVisible = isVisible;

            averagePrice = Formula.Indicator();
            averagePrice.Name = "BE";
            averagePrice.Drawing.IsVisible = isVisible;
            averagePrice.Drawing.Color = Color.Black;

            position = Formula.Indicator();
            position.Name = "Position";
            position.Drawing.PaneType = PaneType.Secondary;
            position.Drawing.IsVisible = isVisible;
        }

        public override bool OnProcessTick(Tick tick)
        {
            Orders.SetAutoCancel();
            if (!tick.IsQuote)
            {
                throw new InvalidOperationException("This strategy requires bid/ask quote data to operate.");
            }

            var comboTrades = Performance.ComboTrades;
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

            if( comboTrades.Count > 0)
            {
                var comboTrade = comboTrades.Tail;
                averagePrice[0] = Position.IsFlat ? double.NaN : CalcAveragePrice(comboTrade);
            }
            else
            {
                averagePrice[0] = double.NaN;
            }

            if (bidLine.Count > 0)
            {
                position[0] = Position.Current / lotSize;
            }
            return true;
        }

        private double lastMidpoint;
        private void OnProcessFlat(Tick tick)
        {
            var midpoint = (tick.Ask + tick.Bid) / 2;
            if (isFirstTick)
            {
                isFirstTick = false;
                lastMidpoint = midpoint;
                SetFlatBidAsk();
            }
            var comboTrades = Performance.ComboTrades;
            if(comboTrades.Count == 0 || comboTrades.Tail.Completed)
            {
                switch( Direction)
                {
                    case Direction.Long:
                        if( midpoint > lastMidpoint + 5*minimumTick)
                        {
                            lastMidpoint = midpoint;
                            SetFlatBidAsk();
                        }
                        Orders.Enter.ActiveNow.BuyLimit(bid, lotSize);
                        break;
                    case Direction.Short:
                        if (midpoint < lastMidpoint - 5 * minimumTick)
                        {
                            lastMidpoint = midpoint;
                            SetFlatBidAsk();
                        }
                        Orders.Enter.ActiveNow.SellLimit(ask, lotSize);
                        break;
                    case Direction.Both:
                        Orders.Enter.ActiveNow.BuyLimit(bid, lotSize);
                        Orders.Enter.ActiveNow.SellLimit(ask, lotSize);
                        break;
                }
            }
            else
            {
                switch (Direction)
                {
                    case Direction.Long:
                        Orders.Change.ActiveNow.BuyLimit(bid, lotSize);
                        break;
                    case Direction.Short:
                        Orders.Change.ActiveNow.SellLimit(ask, lotSize);
                        break;
                    case Direction.Both:
                        Orders.Change.ActiveNow.BuyLimit(bid, lotSize);
                        Orders.Change.ActiveNow.SellLimit(ask, lotSize);
                        break;
                }
            }
        }

        private double CalcAveragePrice(TransactionPairBinary comboTrade)
        {
            var sign = Math.Sign(comboTrade.CurrentPosition);
            var position = comboTrade.CurrentPosition;
            return (comboTrade.AverageEntryPrice * position + sign * comboTrade.ClosedPoints) / position;
        }

        private void OnProcessLong(Tick tick)
        {
            var comboTrade = Performance.ComboTrades.Tail;
            var averageEntry = CalcAveragePrice(comboTrade);
            var lots = Position.Size / lotSize;
            bid = fills.First.Value.Price - CalcIncreaseSpread(tick);
            bidLine[0] = bid;
            Orders.Change.ActiveNow.BuyLimit(bid, lotSize);
            if (lots > thresholdLots)
            {
                if (Direction == Direction.Long)
                {
                    Orders.Exit.ActiveNow.SellLimit(averageEntry + spread);
                }
                else
                {
                    Orders.Reverse.ActiveNow.SellLimit(averageEntry + spread, lotSize);
                }
                return;
            }
            if (averageEntry < ask)
            {
                if( Direction == Direction.Long)
                {
                    Orders.Exit.ActiveNow.SellLimit(ask);
                }
                else
                {
                    Orders.Reverse.ActiveNow.SellLimit(ask, lotSize);
                }
            }
            else if( lots <= thresholdLots)
            {
                Orders.Change.ActiveNow.SellLimit(ask, lotSize);
            }
        }

        private void OnProcessShort(Tick tick)
        {
            var comboTrade = Performance.ComboTrades.Tail;
            var averageEntry = CalcAveragePrice(comboTrade);
            var lots = Position.Size/lotSize;
            ask = fills.First.Value.Price + CalcIncreaseSpread(tick);
            askLine[0] = ask;
            Orders.Change.ActiveNow.SellLimit(ask, lotSize);
            if( lots > thresholdLots)
            {
                if (Direction == Direction.Short)
                {
                    Orders.Exit.ActiveNow.BuyLimit(averageEntry - spread);
                } else {
                    Orders.Reverse.ActiveNow.BuyLimit(averageEntry - spread, lotSize);
                }
                return;
            }
            if (averageEntry > bid)
            {
                if (Direction == Direction.Short)
                {
                    Orders.Exit.ActiveNow.BuyLimit(bid);
                }
                else
                {
                    Orders.Reverse.ActiveNow.BuyLimit(bid, lotSize);
                }
            } else {
                Orders.Change.ActiveNow.BuyLimit(bid, lotSize);
            }
        }

        private double CalcIncreaseSpread(Tick tick)
        {
            var lots = Position.Size / lotSize;
            if( lots < thresholdLots) return spread;
            var newDirection = Position.IsLong ? Direction.Long : Direction.Short;
            if( direction != newDirection)
            {
                direction = newDirection;
                if (onDirectionChange != null) onDirectionChange(this);
            }
            var lastFill = fills.First.Value;
            var prevFill = fills.First.Next.Value;
            var timeSlice = 10;
            var elapsed = tick.Time - lastFill.Time;
            var seconds = Math.Max(0,timeSlice - elapsed.TotalSeconds);
            var lastSpread = spread*2;
            lastSpread = Math.Max(lastSpread, spread);
            if( elapsed.TotalSeconds < timeSlice)
            {
                return lastSpread + (lastSpread*10 / timeSlice) * seconds;
            }
            else
            {
                return lastSpread;
            }

        }

        public override void OnEndHistorical()
        {
            Log.Notice("Total volume was " + totalVolume + ". With commission paid of " + ((totalVolume / 1000) * 0.02D));
        }

        public class LocalFill
        {
            public int Size;
            public double Price;
            public TimeStamp Time;
            public LocalFill( LogicalFill fill)
            {
                Size = Math.Abs(fill.Position);
                Price = fill.Price;
                Time = fill.Time;
            }
            public LocalFill(int size, double price, TimeStamp time)
            {
                Size = size;
                Price = price;
                Time = time;
            }
            public override string ToString()
            {
                return Size + " at " + Price;
            }
        }
        private int lastSize = 0;
        private ActiveList<LocalFill> fills = new ActiveList<LocalFill>();

        private void SetupBidAsk()
        {
            var tick = Ticks[0];
            var currentFill = fills.First.Value;
            var myAsk = currentFill.Price + spread / 2;
            var myBid = currentFill.Price - spread / 2;
            var marketAsk = Math.Max(tick.Ask, tick.Bid);
            var marketBid = Math.Min(tick.Ask, tick.Bid);
            ask = Math.Max(myAsk, marketAsk);
            bid = Math.Min(myBid, marketBid);
            bidLine[0] = bid;
            askLine[0] = ask;
        }

        public override void OnEnterTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            lastSize = Math.Abs(comboTrade.CurrentPosition);
            fills.AddFirst(new LocalFill(fill));
            SetupBidAsk();
        }

        public override void OnChangeTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            var size = Math.Abs(comboTrade.CurrentPosition);
            var change = size - lastSize;
            lastSize = size;
            if (change > 0)
            {
                if (fills.First != null && fill.Price == fills.First.Value.Price)
                {
                    fills.First.Value.Size += change;
                }
                else
                {
                    fills.AddFirst(new LocalFill(change, fill.Price, fill.Time));
                    SetupBidAsk();
                }
            }
            else
            {
                change = Math.Abs(change);
                for (var current = fills.First; current != null; current = current.Next)
                {
                    var prevFill = current.Value;
                    if (change > prevFill.Size)
                    {
                        change -= prevFill.Size;
                        fills.Remove(current);
                        if (fills.Count > 0)
                        {
                            SetupBidAsk();
                        }
                    }
                    else
                    {
                        prevFill.Size -= change;
                        if (prevFill.Size == 0)
                        {
                            fills.Remove(current);
                            if( fills.Count > 0)
                            {
                                SetupBidAsk();
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void SetFlatBidAsk()
        {
            var tick = Ticks[0];
            var midPoint = (tick.Bid + tick.Ask)/2;
            var myAsk = midPoint + spread/2;
            var myBid = midPoint - spread/2;
            var marketAsk = Math.Max(tick.Ask, tick.Bid);
            var marketBid = Math.Min(tick.Ask, tick.Bid);
            ask = Math.Max(myAsk, marketAsk);
            bid = Math.Min(myBid, marketBid);
            bidLine[0] = bid;
            askLine[0] = ask;
        }

        private long totalVolume = 0;
        public override void OnExitTrade(TransactionPairBinary comboTrade, LogicalFill fill, LogicalOrder filledOrder)
        {
            var tick = Ticks[0];
            lastMidpoint = (tick.Ask + tick.Bid)/2;
            fills.Clear();
            if( !comboTrade.Completed)
            {
                throw new InvalidOperationException("Trade must be completed.");
            }
            totalVolume += comboTrade.Volume;
        }
    }
}
