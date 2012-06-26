namespace TickZoom.Api
{
    [SerializeContract]
    public struct PhysicalOrderBinary
    {
        [SerializeMember(1)]
        public int sequence;
        [SerializeMember(2)]
        public OrderAction action;
        [SerializeMember(3)]
        public OrderState orderState;
        [SerializeMember(4)]
        public TimeStamp lastModifyTime;
        [SerializeMember(5)]
        public TimeStamp lastReadTime;
        [SerializeMember(6)]
        public SymbolInfo symbol;
        [SerializeMember(7)]
        public OrderType type;
        [SerializeMember(8)]
        public double price;
        [SerializeMember(9)]
        public int completeSize;
        [SerializeMember(10)]
        public int cumulativeSize;
        [SerializeMember(11)]
        public int remainingSize;
        [SerializeMember(12)]
        public OrderSide side;
        [SerializeMember(13)]
        public int logicalOrderId;
        [SerializeMember(14)]
        public long logicalSerialNumber;
        [SerializeMember(15)]
        public long brokerOrder;
        [SerializeMember(16)]
        public string tag;
        //[SerializeMember(17)]
        public object reference;
        [SerializeMember(18)]
        public PhysicalOrder originalOrder;
        [SerializeMember(19)]
        public PhysicalOrder replacedBy;
        [SerializeMember(20)]
        public TimeStamp utcCreateTime;
        [SerializeMember(21)]
        public OrderFlags orderFlags;
        [SerializeMember(22)]
        public int cancelCount;
        [SerializeMember(23)]
        public int pendingCount;
    }
}