namespace TickZoom.TickUtil
{
    public struct BinaryBlockHeader
    {
        public short version;
        public BinaryBlockType type;
        public int length;
        public long checkSum;
        public long CalcChecksum()
        {
            return (short)type ^ version ^ length;
        }
        public override string ToString()
        {
            return "FileBlock( version " + version + ", type " + type + ", length " + length + ", checksum " +
                   checkSum + ")";
        }
    }
}