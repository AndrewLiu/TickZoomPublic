using System;
using System.IO;
using TickZoom.Api;

namespace TickZoom.TickUtil
{
    public class FileBlock
    {
        private static readonly Log log = Factory.SysLog.GetLogger(typeof (FileBlock));
        private static readonly bool debug = log.IsDebugEnabled;
        private static readonly bool trace = log.IsTraceEnabled;
        private MemoryStream memory = new MemoryStream();
        private SerialDataHeader serialDataHeader;
        private int tickBlockHeaderSize;
        private int blockSize;
        private int dataVersion;
        public unsafe FileBlock(int blockSize)
        {
            if( blockSize == 0)
            {
                throw new ArgumentException("blocksize cannot be zero");
            }
            this.blockSize = blockSize;
            tickBlockHeaderSize = sizeof(SerialDataHeader);
            memory.SetLength(tickBlockHeaderSize);
        }

        public long LastUtcTimeStamp
        {
            get { return serialDataHeader.lastUtcTimeStamp; }
        }

        public bool HasData
        {
            get { return memory.Position > tickBlockHeaderSize; }
        }

        public int DataVersion
        {
            get { return dataVersion; }
        }

        public void ReserveHeader()
        {
            memory.SetLength(tickBlockHeaderSize);
            memory.Position = tickBlockHeaderSize;
        }

        public bool TryWrite(Serializable serializable, long utcTime)
        {
            var result = true;
            var tempPosition = memory.Position;
            serializable.ToWriter(memory);
            if (serialDataHeader.firstUtcTimeStamp == 0L)
            {
                serialDataHeader.firstUtcTimeStamp = utcTime;
            }
            if (memory.Position > blockSize)
            {
                memory.Position = tempPosition;
                memory.SetLength(tempPosition);
                result = false;
            }
            else
            {
                serialDataHeader.lastUtcTimeStamp = utcTime;
            }
            return result;
        }

        public unsafe void JumpToLast(FileStream fs)
        {
            fs.Position = fs.Length - blockSize;
        }

        public unsafe void ReadNextBlock(FileStream fs)
        {
            var tempPosition = fs.Position;
            memory.SetLength(blockSize);
            var buffer = memory.GetBuffer();
            memory.Position = 0;
            while (memory.Position < memory.Length)
            {
                var bytesRead = fs.Read(buffer, (int)memory.Position, (int)(blockSize - memory.Position));
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Cannot read past the end of the stream.");
                }
                memory.Position += bytesRead;
            }

            fixed (byte* bptr = buffer)
            {
                serialDataHeader = *((SerialDataHeader*)bptr);
            }
            memory.Position = tickBlockHeaderSize;
            if (!serialDataHeader.VerifyChecksum())
            {
                var tempLength = fs.Length;
                throw new InvalidOperationException("Tick block header checksum failed at " + tempPosition + ", length " + tempLength + ", current length " + fs.Length + ", current position " + fs.Position + ": " + fs.Name + "\n" + BitConverter.ToString(memory.GetBuffer(), 0, blockSize));
            }
        }

        public unsafe void WriteHeader()
        {
            if (memory.Length < tickBlockHeaderSize)
            {
                throw new InvalidOperationException("Insufficient byte to write tick block header. Expected: " + tickBlockHeaderSize + " but was: " + memory.Length);
            }
            serialDataHeader.blockHeader.type = BinaryBlockType.SerialData;
            serialDataHeader.blockHeader.version = 1;
            serialDataHeader.blockHeader.length = (int)memory.Position;
            serialDataHeader.SetChecksum();
            fixed (byte* bptr = memory.GetBuffer())
            {
                *((SerialDataHeader*)bptr) = serialDataHeader;
            }
        }

        public bool TryRead(Serializable tickIO)
        {
            try
            {
                if( memory.Position >= serialDataHeader.blockHeader.length)
                {
                    return false;
                }
                dataVersion = tickIO.FromReader(memory);
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
            catch( IndexOutOfRangeException)
            {
                return false;
            }
        }

        public void Write(FileStream fs)
        {
            var errorCount = 0;
            var sleepSeconds = 3;
            do
            {
                try
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG342, memory.Position);
                    WriteHeader();
                    if (memory.Length < blockSize)
                    {
                        memory.SetLength(blockSize);
                    }
                    fs.Write(memory.GetBuffer(), 0, (int)memory.Length);
                    fs.Flush();
                    memory.Position = 0;
                    if (errorCount > 0)
                    {
                        log.Notice("Retry successful.");
                    }
                    errorCount = 0;
                }
                catch (IOException e)
                {
                    errorCount++;
                    log.DebugFormat(LogMessage.LOGMSG343, e.Message, sleepSeconds);
                    Factory.Parallel.Sleep(3);
                }
            } while (errorCount > 0);
        }
    }
}