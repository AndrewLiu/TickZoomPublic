using TickZoom.Api;

namespace TickZoom.TickUtil
{
    public struct SerialDataHeader
    {
        public BinaryBlockHeader blockHeader;
        public long firstUtcTimeStamp;
        public long lastUtcTimeStamp;
        public long checkSum;
        public bool VerifyChecksum()
        {
            var expectedChecksum = CalcChecksum();
            return expectedChecksum == checkSum;
        }
        public void SetChecksum()
        {
            checkSum = CalcChecksum();
        }
        private long CalcChecksum()
        {
            return blockHeader.CalcChecksum() ^ firstUtcTimeStamp ^ lastUtcTimeStamp;
        }
        public override string ToString()
        {
            return blockHeader + ", first " + new TimeStamp(firstUtcTimeStamp) + ", last " + new TimeStamp(lastUtcTimeStamp) +
                   ", checksum " + checkSum;
        }
    }
}