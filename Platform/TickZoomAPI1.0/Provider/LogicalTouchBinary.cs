using System.Text;

namespace TickZoom.Api
{
    public struct LogicalTouchBinary : LogicalTouch
    {
        private int orderId;
        private long orderSerialNumber;
        public LogicalTouchBinary(int orderId, long orderSerialNumber)
        {
            this.orderId = orderId;
            this.orderSerialNumber = orderSerialNumber;
        }

        public int OrderId
        {
            get { return orderId; }
        }

        public long OrderSerialNumber
        {
            get { return orderSerialNumber; }
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