using System.Text;

namespace TickZoom.Api
{
    public struct LogicalTouchBinary : LogicalTouch
    {
        private int orderId;
        private long orderSerialNumber;
        private long recency;
        private TimeStamp utcTime;

        public LogicalTouchBinary(int orderId, long orderSerialNumber, long recency, TimeStamp utcTime)
        {
            this.orderId = orderId;
            this.orderSerialNumber = orderSerialNumber;
            this.recency = recency;
            this.utcTime = utcTime;
        }

        public int OrderId
        {
            get { return orderId; }
        }

        public long OrderSerialNumber
        {
            get { return orderSerialNumber; }
        }

        public long Recency
        {
            get { return recency; }
        }

        public TimeStamp UtcTime
        {
            get { return utcTime; }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(orderId);
            sb.Append(",");
            sb.Append(orderSerialNumber);
            sb.Append(",");
            sb.Append(utcTime);
            return sb.ToString();
        }
    }
}