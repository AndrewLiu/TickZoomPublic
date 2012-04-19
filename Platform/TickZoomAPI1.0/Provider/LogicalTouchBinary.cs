using System.Text;

namespace TickZoom.Api
{
    public struct LogicalTouchBinary : LogicalTouch
    {
        private int orderId;
        private long orderSerialNumber;
        private long recency;

        public LogicalTouchBinary(int orderId, long orderSerialNumber, long recency)
        {
            this.orderId = orderId;
            this.orderSerialNumber = orderSerialNumber;
            this.recency = recency;
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

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(orderId);
            sb.Append(",");
            sb.Append(orderSerialNumber);
            return sb.ToString();
        }
    }
}