using System;
using System.Threading;

namespace TickZoom.Api
{
    public struct TransactionPairBinary 
    {
        private static readonly Log log = Factory.SysLog.GetLogger(typeof(TransactionPairBinary));
        private static readonly bool debug = log.IsDebugEnabled;
        private static readonly bool trace = log.IsTraceEnabled;
        private static readonly bool verbose = log.IsVerboseEnabled;
        public static readonly string TIMEFORMAT = "yyyy-MM-dd HH:mm:ss.fff";
        private int entryOrderId;
        private long entrySerialNumber;
        private int exitOrderId;
        private long exitSerialNumber;
        private long entryTime;
        private long postedEntryTime;
        private long exitTime;
        private long postedExitTime;
        private int currentPosition;
        private int shortVolume;
        private int longVolume;
        private double closedPoints;
        private double entryPrice;
        private double averageEntryPrice;
        private double exitPrice;
        private double maxPrice;
        private double minPrice;
        private int entryBar;
        private int exitBar;
        private bool completed;
		
        public static TransactionPairBinary Parse(string value) {
            TransactionPairBinary pair = new TransactionPairBinary();
            string[] fields = value.Split(',');
            int field = 0;
            pair.currentPosition = int.Parse(fields[field++]);
            pair.entryOrderId = int.Parse(fields[field++]);
            pair.entrySerialNumber = long.Parse(fields[field++]);
            pair.entryBar = int.Parse(fields[field++]);
            pair.entryPrice = double.Parse(fields[field++]);
            pair.entryTime = TimeStamp.Parse(fields[field++]).Internal;
            pair.postedEntryTime = TimeStamp.Parse(fields[field++]).Internal;
            pair.exitOrderId = int.Parse(fields[field++]);
            pair.exitSerialNumber = long.Parse(fields[field++]);
            pair.exitBar = int.Parse(fields[field++]);
            pair.exitPrice = double.Parse(fields[field++]);
            pair.exitTime = TimeStamp.Parse(fields[field++]).Internal;
            pair.postedExitTime = TimeStamp.Parse(fields[field++]).Internal;
            pair.maxPrice = double.Parse(fields[field++]);
            pair.minPrice = double.Parse(fields[field++]);
            pair.longVolume = int.Parse(fields[field++]);
            pair.shortVolume = int.Parse(fields[field++]);
            return pair;
        }

        public TransactionPairBinary(TransactionPairBinary other)
        {
            entryTime = other.entryTime;
            postedEntryTime = other.postedEntryTime;
            exitTime = other.exitTime;
            postedExitTime = other.postedExitTime;
            currentPosition = other.currentPosition;
            entryPrice = other.entryPrice;
            averageEntryPrice = other.averageEntryPrice;
            exitPrice = other.exitPrice;
            minPrice = other.minPrice;
            maxPrice = other.maxPrice;
            closedPoints = other.ClosedPoints;
            exitBar = other.exitBar;
            entryBar = other.entryBar;
            completed = other.completed;
            longVolume = other.LongVolume;
            shortVolume = other.ShortVolume;
            entryOrderId = other.entryOrderId;
            entrySerialNumber = other.entrySerialNumber;
            exitOrderId = other.exitOrderId;
            exitSerialNumber = other.entrySerialNumber;
        }

        public override string ToString()
        {
            return Direction + "," + entryOrderId + "," + entrySerialNumber + "," + entryBar + "," + entryPrice + "," + new TimeStamp(entryTime) + "," + new TimeStamp(postedEntryTime) + "," +
                   exitOrderId + "," + exitSerialNumber + "," + exitBar + "," + exitPrice + "," + new TimeStamp(exitTime) + "," + new TimeStamp(postedExitTime) + "," + maxPrice + "," + minPrice + "," + LongVolume + "," + ShortVolume + "," + currentPosition;
        }
		
		
        public bool Completed {
            get { return completed; }
        }
		
        public static TransactionPairBinary Create() {
            return new TransactionPairBinary();
        }
		
        public void TryUpdate(Tick tick) {
            if( !completed) {
                if( currentPosition > 0)
                {
                    var price = tick.IsQuote ? tick.Bid : tick.Price;
                    if( verbose) log.VerboseFormat("Setting using {0} of {1}", price, tick);
                    UpdatePrice(price);
                } else {
                    var price = tick.IsQuote ? tick.Ask : tick.Price;
                    if( verbose) log.VerboseFormat("Setting using {0} of {1}", price, tick);
                    UpdatePrice(price);
                }
                exitTime = tick.Time.Internal;
            }
        }
		
		
        public void UpdatePrice(double price) {
            if (price > maxPrice)
            {
                if( verbose) log.VerboseFormat("UpdatePrice( max = {0})", price);
                maxPrice = price;
            }
            if( price < minPrice) {
                if( verbose) log.VerboseFormat("UpdatePrice( min = {0})", price);
                minPrice = price;
            }
            exitPrice = price;
        }
		
        public void Enter( int direction, double price, TimeStamp time, TimeStamp postedTime, int bar, int entryOrderId, long entrySerialNumber) {
            this.currentPosition = direction;
            if( currentPosition > 0) {
                this.longVolume = Math.Abs(direction);
            } else {
                this.shortVolume = Math.Abs(direction);
            }
            if( trace) log.TraceFormat("Enter long volume = {0}, short volume = {1}", this.LongVolume, this.ShortVolume);
            this.entryPrice = price;
            this.averageEntryPrice = price;
            this.maxPrice = this.minPrice = averageEntryPrice;
            this.entryTime = time.Internal;
            this.postedEntryTime = postedTime.Internal;
            this.entryBar = bar;
            this.entryOrderId = entryOrderId;
            this.entrySerialNumber = entrySerialNumber;
        }
		
        public void Exit( double price, TimeStamp time, TimeStamp postedTime, int bar, int exitOrderId, long exitSerialNumber) {
            ChangeSize(0, price);
            if( currentPosition < 0) {
                this.longVolume = this.LongVolume + Math.Abs( currentPosition);
            } else {
                this.shortVolume = this.ShortVolume + Math.Abs( currentPosition);
            }
            if( trace) log.TraceFormat("Exit long volume = {0}, short volume = {1}, Direction = {2}", this.LongVolume, ShortVolume, this.Direction);
            this.exitPrice = price;
            this.exitTime = time.Internal;
            this.postedExitTime = postedTime.Internal;
            this.exitBar = bar;
            this.completed = true;
            this.exitOrderId = exitOrderId;
            this.exitSerialNumber = exitSerialNumber;
        }
		
        public void Update( double price, TimeStamp time, int bar) {
            this.exitPrice = price;
            this.exitTime = time.Internal;
            this.exitBar = bar;
        }
		
        public void ChangeSize( int newSize, double price) {
            var sizeChange = newSize - currentPosition;      // -6666
            if (currentPosition >= 0 && sizeChange > 0)
            {
                longVolume = LongVolume + Math.Abs(sizeChange);
            }
            else if( currentPosition <= 0 && sizeChange < 0)
            {
                shortVolume = ShortVolume + Math.Abs(sizeChange);
            }

            if( currentPosition == 0)
            {
                averageEntryPrice = price;
            }
            else if (Math.Abs(newSize) > Math.Abs(currentPosition))
            {
                var sum = averageEntryPrice.ToLong() * currentPosition; // 1951840000000000
                var sum2 = sizeChange * price.ToLong();    // -650394954000000
                var newPrice = ((sum + sum2) / newSize).ToDouble();     // 97603498275
                averageEntryPrice = newPrice;
            }
            else
            {
                closedPoints -= (price - averageEntryPrice) * sizeChange;
            }
            currentPosition = newSize;

            if( trace) log.TraceFormat("Price = {0}, averageEntryPrice = {1}, CurrentPosition = {2}, NewSize = {3}, Direction = {4}, sizeChange = {5}, Long volume = {6}, short volume = {7}", price, averageEntryPrice, currentPosition, newSize, Direction, sizeChange, this.LongVolume, ShortVolume);
        }

        public void Change(double price, int positionChange)
        {
            var newPosition = currentPosition + positionChange;
            if (currentPosition == 0)
            {
                Enter(newPosition, price, Factory.Parallel.UtcNow, Factory.Parallel.UtcNow, 0, 0, 0);
            }
            else if (newPosition == 0)
            {
                Exit(price, Factory.Parallel.UtcNow, Factory.Parallel.UtcNow, 0, 0, 0);
            }
            else
            {
                ChangeSize(newPosition, price);
            }
            UpdatePrice(price);
        }

        public int Direction
        {
            get {
                return LongVolume - ShortVolume;
            }
        }
		
        public double EntryPrice {
            get { return entryPrice; }
        }
		
        public double ExitPrice {
            get { return exitPrice; }
            set { exitPrice = value;  }
        }
		
        [Obsolete("Please use TransactionPairs.GetProfitLoss() instead.",true)]
        public double ProfitLoss {
            get { return 0.0; }
        }
		
        public string ToStringHeader() {
            return "Direction,EntryBar,EntryPrice,EntryTime,ExitPrice,ExitBar,ExitTime,MaxPrice,MinPrice,ProfitLoss";
        }
		
        public override int GetHashCode() {
            string hash = Direction + ":" + EntryPrice + EntryTime + ExitPrice + ExitTime;
            return hash.GetHashCode();
        }
        public override bool Equals(object obj) {
            if( obj.GetType() != typeof(TransactionPairBinary)) {
                return false;
            }
            TransactionPairBinary trade = (TransactionPairBinary) obj;
            var entryPriceMatch = this.currentPosition == trade.currentPosition ? this.averageEntryPrice == trade.averageEntryPrice : true;
            return this.LongVolume == trade.LongVolume &&
                   this.ShortVolume == trade.ShortVolume &&
                   entryPriceMatch &&
                   this.EntryTime == trade.EntryTime &&
                   this.exitPrice == trade.exitPrice &&
                   this.ExitTime == trade.ExitTime;
        }
		
        public double MaxPrice {
            get { return maxPrice; }
        }
		
        public double MinPrice {
            get { return minPrice; }
        }
		
        public int EntryBar {
            get { return entryBar; }
        }
		
        public int ExitBar {
            get { return exitBar; }
        }
		
        public TimeStamp EntryTime {
            get { return new TimeStamp(entryTime); }
        }
		
        public TimeStamp ExitTime {
            get { return new TimeStamp(exitTime); }
        }
		
        public int Volume {
            get { return LongVolume + ShortVolume; }
        }
		
        public int EntryOrderId {
            get { return entryOrderId; }
        }
		
        public int ExitOrderId {
            get { return exitOrderId; }
        }
		
        public double AverageEntryPrice {
            get { return averageEntryPrice; }
        }
		
        public int CurrentPosition {
            get { return currentPosition; }
        }

        public double ClosedPoints
        {
            get { return closedPoints; }
            set { closedPoints = value; }
        }

        public int LongVolume
        {
            get { return longVolume; }
        }

        public int ShortVolume
        {
            get { return shortVolume; }
        }
    }
}