using System;
using TickZoom.Api;

namespace TickZoom.Examples
{
    public class InventoryGroupDefault : InventoryGroup
    {
        private static Log log = Factory.SysLog.GetLogger(typeof (InventoryGroupDefault));
        private bool debug = log.IsDebugEnabled;
        private bool trace = log.IsTraceEnabled;
        private int id;
        private TransactionPairBinary binary;
        private double breakEven = double.NaN;
        private double retrace = 0.60D;
        private int profitTicks = 20;
        private SymbolInfo symbol;
        private ProfitLoss2 profitLoss;
        private int roundLotSize = 1;
        private int startingLotSize = 1;
        private int minimumLotSize;
        private int maximumLotSize = int.MaxValue;
        private int _goal = 1;
        private double currentProfit;
        private double cumulativeProfit;
        private InventoryType type = InventoryType.Either;
        private InventoryStatus status = InventoryStatus.Flat;
        private double maxSpread;
        private double increaseSpread;
        private double decreaseSpread;
        private double favorableExcursion;
        private double adverseExcursion;

        public InventoryGroupDefault(SymbolInfo symbol) : this(symbol, 1)
        {
        }

        public InventoryGroupDefault(SymbolInfo symbol, int id)
        {
            IncreaseSpread = 10 * symbol.MinimumTick;
            DecreaseSpread = 5 * symbol.MinimumTick;
            this.symbol = symbol;
            this.id = id;
            profitLoss = symbol.ProfitLoss as ProfitLoss2;
            if (profitLoss == null)
            {
                var message = "Requires ProfitLoss2 interface for calculating profit and loss.";
                log.Error(message);
                throw new ApplicationException(message);
            }
        }

        public void Clear()
        {
            binary = default(TransactionPairBinary);
            breakEven = double.NaN;
            currentProfit = 0D;
            cumulativeProfit = 0D;
            type = InventoryType.Either;
            status = InventoryStatus.Flat;
            maxSpread = 0D;
            adverseExcursion = favorableExcursion = 0D;
        }

        public void UpdateBidAsk(double marketBid, double marketAsk)
        {
            if (binary.CurrentPosition > 0)
            {
                binary.UpdatePrice(marketAsk);
                TrackExcursion(marketAsk);
            }
            if (binary.CurrentPosition < 0)
            {
                binary.UpdatePrice(marketBid);
                TrackExcursion(marketBid);
            }
        }

        public double EntryPrice
        {
            get { return binary.EntryPrice; }
        }

        public double ExtremePrice
        {
            get
            {
                if (binary.CurrentPosition > 0)
                {
                    return binary.MinPrice;
                }
                if (binary.CurrentPosition < 0)
                {
                    return binary.MaxPrice;
                }
                return 0D;
            }
        }

        public void CalculateLongBidOffer(double marketBid, double marketOffer)
        {
            var midPoint = (marketBid + marketOffer)/2;
            bid = Math.Min(midPoint, binary.MinPrice - IncreaseSpread);
            bidSize = QuantityToChange(bid);
            if( bidSize < minimumLotSize)
            {
                bidSize = minimumLotSize;
                bid = PriceToChange(bidSize);
                bid = Math.Min(bid, midPoint);
            }
            bidSize = Math.Max(bidSize, minimumLotSize);
            bidSize = Math.Min(bidSize, maximumLotSize);

            offerSize = minimumLotSize;
            offer = PriceToChange(-offerSize);
            offer = Math.Max(offer, midPoint);

            var priceToClose = PriceToClose();
            if (offer >= priceToClose)
            {
                offer = priceToClose;
                offerSize = Math.Abs(Size);
            }

            bid = Math.Min(bid, BreakEven - IncreaseSpread);
            offer = Math.Max(offer, bid + DecreaseSpread);

            if (trace)
                log.TraceFormat(LogMessage.LOGMSG712, BreakEven, binary.MinPrice, bid, offer, binary.CurrentPosition);
            if (binary.CurrentPosition != 0)
            {
                AssertGreaterOrEqual(Round(BreakEven), Round(bid), "break even >= bid");
                AssertGreater(offer, bid, "offer > bid");
            }
            var spread = offer - bid;
            if (spread > maxSpread)
            {
                maxSpread = spread;
            }

            if (additionalTicks > 0)
            {
                bid -= additionalTicks*symbol.MinimumTick;
            }
        }

        private void AssertGreater(double expected, double actual, string message)
        {
            if (expected <= actual)
            {
                var error = "Expected " + expected + " greater than " + actual + " for " + message;
                log.Error(error);
                throw new ApplicationException(error);
            }
        }

        private void AssertGreaterOrEqual(double expected, double actual, string message)
        {
            if (expected < actual)
            {
                var error = "Expected " + expected + " greater than or equal to " + actual + " for " + message;
                log.Error(error);
                throw new ApplicationException(error);
            }
        }

        private void AssertLessThan(double expected, double actual, string message)
        {
            if (expected >= actual)
            {
                var error = "Expected " + expected + " less then " + actual + " for " + message;
                log.Error(error);
                throw new ApplicationException(error);
            }
        }

        public void CalculateShortBidOffer(double marketBid, double marketOffer)
        {
            offer = Math.Max(marketOffer, binary.MaxPrice + IncreaseSpread);
            offerSize = QuantityToChange(offer);
            if( offerSize > -minimumLotSize)
            {
                offerSize = -minimumLotSize;
                offer = PriceToChange(offerSize);
                offer = Math.Max(marketOffer, offer);
            }
            offerSize = Math.Min(offerSize, -minimumLotSize);
            offerSize = Math.Max(offerSize, -maximumLotSize);

            bidSize = minimumLotSize;
            bid = PriceToChange(bidSize);
            bid = Math.Min(bid, marketBid);

            var priceToClose = PriceToClose();
            if (bid <= priceToClose)
            {
                bid = priceToClose;
                bidSize = Math.Abs(Size);
            }

            offer = Math.Max(offer, BreakEven + IncreaseSpread);
            bid = Math.Min(bid, offer - DecreaseSpread);

            if (trace)
                log.TraceFormat(LogMessage.LOGMSG713, BreakEven, binary.MaxPrice, bid, offer,
                                binary.CurrentPosition, marketBid, marketOffer);
            if (binary.CurrentPosition != 0)
            {
                AssertGreaterOrEqual(Round(offer), Round(BreakEven), "break even > bid");
                AssertGreater(offer, bid, "offer > bid");
                //AssertGreaterOrEqual(binary.MaxPrice, breakEven, "MaxPrice >= break even");
            }
            var spread = offer - bid;
            if (spread > maxSpread)
            {
                maxSpread = spread;
            }

            if (additionalTicks > 0)
            {
                offer += additionalTicks*symbol.MinimumTick;
            }
        }

        public double PriceToChange(int quantity)
        {
            if( Math.Abs(binary.CurrentPosition) == minimumLotSize)
            {
                return CalcSpreadPrice(quantity);
            }
            var retraceComplement = 1 - retrace;
            var totalValue = (binary.CurrentPosition*breakEven);
            var startingPrice = binary.EntryPrice;
            var upper = retrace*startingPrice*quantity + retrace*startingPrice*binary.CurrentPosition - totalValue;
            var lower = retrace*quantity - retraceComplement*binary.CurrentPosition;
            if( lower == 0D)
            {
                return CalcSpreadPrice(quantity);
            }
            var result = upper/lower;
            return result;
        }

        private double CalcSpreadPrice(int quantity)
        {
            var signPosition = Math.Sign(binary.CurrentPosition);
            var signQuantity = Math.Sign(quantity);
            var spread = signPosition == signQuantity ? IncreaseSpread : DecreaseSpread;
            var sign = -signPosition;
            return breakEven +  sign * spread;
        }

        public int QuantityToChange(double price)
        {
            if (double.IsNaN(breakEven)) return 0;
            var favorableExcursion = binary.CurrentPosition > 0
                                         ? Math.Max(binary.MaxPrice, price)
                                         : Math.Min(binary.MinPrice, price);
            var adverseExcursion = binary.CurrentPosition > 0
                                       ? Math.Min(price, binary.MinPrice)
                                       : Math.Max(price, binary.MaxPrice);
            var retraceComplement = 1 - retrace;
            var r_favorable = retrace*favorableExcursion;
            var r_adverse = retraceComplement*adverseExcursion;

            var upper = binary.CurrentPosition*(r_favorable + r_adverse - breakEven);
            var lower = retrace * (binary.CurrentPosition > 0 ? favorableExcursion - adverseExcursion : adverseExcursion - favorableExcursion);
            lower = retrace * (adverseExcursion - favorableExcursion);
            if (lower == 0D)
            {
                return 0;
            }
            var quantity = upper / lower;
            return (int)quantity;
        }

        public double BreakEven
        {
            get { return breakEven; }
        }

        private void TrackExcursion(double price)
        {
            var signedExcursion = (price - breakEven) * Math.Sign(binary.CurrentPosition);
            excursion = Math.Abs(signedExcursion);
            if( signedExcursion > favorableExcursion)
            {
                favorableExcursion = excursion;
            }
            if (-signedExcursion > adverseExcursion)
            {
                adverseExcursion = excursion;
            }
        }

        private int additionalTicks = 0;

        public void Change(double price, int positionChange)
        {
            var newPosition = binary.CurrentPosition + positionChange;
            binary.Change(price, positionChange);
            if (trace) log.TraceFormat(LogMessage.LOGMSG714, positionChange, price, binary.CurrentPosition);
            CalcBreakEven();
            TrackExcursion(price);
            if (newPosition == 0)
            {
                breakEven = double.NaN;
                var pandl = CalcProfit(binary, price);
                cumulativeProfit = CumulativeProfit + pandl;
                currentProfit = 0D;
                binary = default(TransactionPairBinary);
                breakEven = double.NaN;
            }
            else
            {
                currentProfit = profitLoss.CalculateProfit(binary.CurrentPosition, binary.AverageEntryPrice, price);
            }
            binary.UpdatePrice(price);
            SetStatus();
        }

        public void CalcBreakEven()
        {
            var size = Math.Abs(binary.CurrentPosition);
            if (size == 0)
            {
                breakEven = double.NaN;
                return;
            }
            var sign = -Math.Sign(binary.CurrentPosition);
            var openPoints = binary.AverageEntryPrice.ToLong()*size;
            var closedPoints = binary.ClosedPoints.ToLong()*sign;
            var grossProfit = openPoints + closedPoints;
            var transaction = 0; // size * commission * sign;
            var expectedTransaction = 0; // size * commission * sign;
            var result = (grossProfit - transaction - expectedTransaction)/size;
            result = ((result + 5000)/10000)*10000;
            breakEven = result.ToDouble();
            var startPrice = binary.EntryPrice;
            var endPrice = binary.CurrentPosition > 0 ? binary.MinPrice : binary.MaxPrice;
            var range = endPrice - startPrice;
            if( startPrice == breakEven)
            {
                if (binary.CurrentPosition > 0)
                {
                    profitTarget = breakEven + decreaseSpread;
                }
                else
                {
                    profitTarget = breakEven - decreaseSpread;
                }
            }
            else
            {
                profitTarget = (1 - profitRetrace) * range + startPrice;
            }
        }

        public double PriceToClose()
        {
            if (binary.CurrentPosition > 0)
            {
                return BreakEven + ProfitTicks*symbol.MinimumTick;
            }
            if (binary.CurrentPosition < 0)
            {
                return BreakEven - ProfitTicks*symbol.MinimumTick;
            }
            throw new InvalidOperationException("Inventory must be long or short to calculate PriceToClose");
        }

        public int HowManyToClose(double price)
        {
            if (binary.CurrentPosition > 0)
            {
                var closePrice = BreakEven + ProfitTicks*symbol.MinimumTick;
                if (price > closePrice)
                {
                    return binary.CurrentPosition;
                }
            }
            if (binary.CurrentPosition < 0)
            {
                var closePrice = BreakEven - ProfitTicks*symbol.MinimumTick;
                if (price < closePrice)
                {
                    return binary.CurrentPosition;
                }
            }
            return 0;
        }

        private int Clamp(int size)
        {
            size = Math.Abs(size);
            size = size < MinimumLotSize ? 0 : Math.Min(MaximumLotSize, size);
            size = size/RoundLotSize*RoundLotSize;
            return size;
        }

        public string ToHeader()
        {
            return "Type#" + id + ",Status#" + id + ",Bid#" + id + ",Offer#" + id + ",Spread#" + id + ",BidQuantity#" +
                   id + ",OfferCuantity#" + id +
                   ",Position#" + id + ",Extreme#" + id + ",BreakEven#" + id + ",PandL#" + id + ",CumPandL#" + id + "";
        }

        public override string ToString()
        {
            return Type + "," + Status + "," + Round(bid) + "," + Round(offer) + "," + Round(offer - bid) + "," +
                   bidSize + "," + offerSize + "," +
                   binary.CurrentPosition + "," + Round(BreakEven) + "," +
                   Round(currentProfit) + "," + Round(cumulativeProfit);
        }

        public double Round(double price)
        {
            return Math.Round(price, symbol.MinimumTickPrecision);
        }

        private void SetStatus()
        {
            if (binary.CurrentPosition == 0)
            {
                status = InventoryStatus.Flat;
            }
            else if (binary.Completed)
            {
                status = InventoryStatus.Complete;
            }
            else if (binary.CurrentPosition > 0)
            {
                status = InventoryStatus.Long;
            }
            else
            {
                status = InventoryStatus.Short;
            }
        }

        public void Pause()
        {
            if (status != InventoryStatus.Flat)
            {
                throw new InvalidOperationException("Inventory must be flat in order to be paused.");
            }
            status = InventoryStatus.Paused;
        }

        public void Resume()
        {
            if (status != InventoryStatus.Paused)
            {
                throw new InvalidOperationException("Inventory must be paused in order to be resumed.");
            }
            status = InventoryStatus.Flat;
        }

        public InventoryStatus Status
        {
            get { return status; }
        }

        public int Size
        {
            get { return binary.CurrentPosition; }
        }

        public int MinimumLotSize
        {
            get { return minimumLotSize; }
            set { minimumLotSize = value; }
        }

        public int MaximumLotSize
        {
            get { return maximumLotSize; }
            set { maximumLotSize = value; }
        }

        public int RoundLotSize
        {
            get { return roundLotSize; }
            set { roundLotSize = value; }
        }

        public double Retrace
        {
            get { return retrace; }
            set { retrace = value; }
        }

        public int Goal
        {
            get { return _goal; }
            set { _goal = value; }
        }

        public int StartingLotSize
        {
            get { return startingLotSize; }
            set { startingLotSize = value; }
        }

        public double CumulativeProfit
        {
            get { return cumulativeProfit; }
        }

        public int ProfitTicks
        {
            get { return profitTicks; }
            set { profitTicks = value; }
        }

        public int BidSize
        {
            get { return bidSize; }
        }

        public int OfferSize
        {
            get { return offerSize; }
        }

        public double Offer
        {
            get { return offer; }
        }

        public double Bid
        {
            get { return bid; }
        }

        public InventoryType Type
        {
            get { return type; }
            set { type = value; }
        }

        public double IncreaseSpread
        {
            get { return increaseSpread; }
            set { increaseSpread = value; }
        }

        public double DecreaseSpread
        {
            get { return decreaseSpread; }
            set { decreaseSpread = value; }
        }

        public double MinPrice
        {
            get { return binary.MinPrice; }
        }

        public double MaxPrice
        {
            get { return binary.MaxPrice; }
        }

        public double ProfitTarget
        {
            get { return profitTarget; }
            set { profitTarget = value; }
        }

        public double ProfitRetrace
        {
            get { return profitRetrace; }
            set { profitRetrace = value; }
        }

        public double AdverseExcursion
        {
            get { return adverseExcursion; }
        }

        public double FavorableExcursion
        {
            get { return favorableExcursion; }
        }

        public double Excursion
        {
            get { return excursion; }
        }

        private double bid;
        private double offer;
        private int offerSize;
        private int bidSize;
        private double profitTarget;
        private double profitRetrace;
        private double excursion;

        public void CalculateBidOffer(double marketBid, double marketOffer)
        {
            bid = marketBid;
            offer = marketOffer;
            bidSize = 0;
            offerSize = 0;
            switch (Status)
            {
                case InventoryStatus.Paused:
                    return;
                case InventoryStatus.Flat:
                    switch (type)
                    {
                        case InventoryType.Short:
                            bidSize = 0;
                            offerSize = startingLotSize;
                            break;
                        case InventoryType.Long:
                            bidSize = startingLotSize;
                            offerSize = 0;
                            break;
                        case InventoryType.Either:
                            bidSize = startingLotSize;
                            offerSize = startingLotSize;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("Unexpected inventory type: " + type);
                    }
                    return;
                case InventoryStatus.Long:
                    CalculateLongBidOffer(marketBid, marketOffer);
                    return;
                case InventoryStatus.Short:
                    CalculateShortBidOffer(marketBid, marketOffer);
                    return;
                case InventoryStatus.Complete:
                default:
                    throw new InvalidOperationException("Unexpected status: " + Status);
            }
        }

        private double CalcProfit(TransactionPairBinary binary, double price)
        {
            double profit;
            double exit;
            profitLoss.CalculateProfit(binary, out profit, out exit);
            return profit;
        }

        public double CurrentProfitLoss(double price)
        {
            currentProfit = CalcProfit(binary, price);
            return currentProfit;
        }
    }
}