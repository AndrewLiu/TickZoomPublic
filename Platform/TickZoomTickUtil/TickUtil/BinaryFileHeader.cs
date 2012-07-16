namespace TickZoom.TickUtil
{
    public struct BinaryFileHeader
    {
        public BinaryBlockHeader blockHeader;
        public long utcTimeStamp;
        public long checkSum;
        public int blockSize;
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
            return blockHeader.CalcChecksum() ^ blockSize ^ utcTimeStamp;
        }
    }
}