using TickZoom.Api;

namespace TickZoom.Common
{
    public class PhysicalOrderQueue
    {
        private static readonly Log staticLog = Factory.SysLog.GetLogger(typeof(PhysicalOrderQueue));
        private readonly bool trace = staticLog.IsTraceEnabled;
        private readonly bool debug = staticLog.IsDebugEnabled;
        private Log log;
        private ActiveList<PhysicalOrder> createOrderQueue = new ActiveList<PhysicalOrder>();
        private ActiveList<PhysicalOrder> cancelOrderQueue = new ActiveList<PhysicalOrder>();

        public PhysicalOrderQueue(string name, SymbolInfo symbol)
        {
            this.log = Factory.SysLog.GetLogger(typeof(PhysicalOrderQueue).FullName + "." + symbol.ExpandedSymbol.StripInvalidPathChars() + "." + name);
        }

        public Iterable<PhysicalOrder> CreateOrderQueue
        {
            get { return createOrderQueue; }
        }

        private bool HasCreateOrder(PhysicalOrder order)
        {
            for (var current = CreateOrderQueue.First; current != null; current = current.Next)
            {
                var queueOrder = current.Value;
                if (order.LogicalSerialNumber == queueOrder.LogicalSerialNumber)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG542, queueOrder);
                    return true;
                }
            }
            return false;
        }

        private bool HasCancelOrder(PhysicalOrder order)
        {
            for (var current = cancelOrderQueue.First; current != null; current = current.Next)
            {
                var clientId = current.Value;
                if (order.OriginalOrder.BrokerOrder == clientId.OriginalOrder.BrokerOrder)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG546, order);
                    return true;
                }
            }
            return false;
        }

        public bool AddCreateOrder(PhysicalOrder order)
        {
            var result = !HasCreateOrder(order);
            if( !result)
            {
                createOrderQueue.AddLast(order);
            }
            return result;
        }

        public bool AddCancelOrder(PhysicalOrder order)
        {
            var result = !HasCancelOrder(order);
            if (!result)
            {
                cancelOrderQueue.AddLast(order);
            }
            return result;
        }

        public void Clear()
        {
            createOrderQueue.Clear();
            cancelOrderQueue.Clear();
        }
    }
}