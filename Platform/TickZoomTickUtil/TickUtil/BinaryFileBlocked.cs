using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using TickZoom.Api;

namespace TickZoom.TickUtil
{
    public class BinaryFileBlocked : BinaryFile
    {
        private string name;
        private int dataVersion;
        private FileStream fs = null;
        private bool quietMode;
        private string fileName;
        private Log log;
        private bool debug;
        private bool trace;
        string appDataFolder;
        string priceDataFolder;
        private bool eraseFileToStart = false;
        private TaskLock memoryLocker = new TaskLock();
        private Action writeFileAction;
        private BinaryFileMode mode;
        private long maxCount = long.MaxValue;
        private TimeStamp startTime = TimeStamp.MinValue;
        private TimeStamp endTime = TimeStamp.MaxValue;
        private bool endOfData = true;
        private FileBlock fileBlock;
        private long startCount;
        private bool isInitialized;
        private StackTrace constructorTrace;
        private Stopwatch readFileStopwatch;
        private long nextProgressUpdateSecond;
        private static object locker = new object();
        private long recordsCount;
        private bool isFirstWrite = true;
        private static List<BinaryFileBlocked> binaryFiles = new List<BinaryFileBlocked>();

        public unsafe BinaryFileBlocked()
        {
            var property = "PriceDataFolder";
            priceDataFolder = Factory.Settings[property];
            if (priceDataFolder == null)
            {
                throw new ApplicationException("Must set " + property + " property in app.config");
            }
            property = "AppDataFolder";
            appDataFolder = Factory.Settings[property];
            if (appDataFolder == null)
            {
                throw new ApplicationException("Must set " + property + " property in app.config");
            }
            writeFileAction = WriteToFile;
            lock( locker)
            {
                binaryFiles.Add(this);
            }
            constructorTrace = new StackTrace(true);
        }

        private void InitLogging()
        {
            log = Factory.SysLog.GetLogger(typeof(BinaryFileBlocked) + "." + mode + "." + name);
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
        }

        public void Initialize(string folderOrfile, string symbolFile, BinaryFileMode mode)
        {
            string[] symbolParts = symbolFile.Split(new char[] { '.' });
            string _symbol = symbolParts[0];
            this.mode = mode;
            name = Factory.Symbol.LookupSymbol(_symbol).ExpandedSymbol.StripInvalidPathChars();
            InitLogging();
            var dataFolder = folderOrfile.Contains(@"Test\") ? appDataFolder : priceDataFolder;
            var filePath = dataFolder + "\\" + folderOrfile;
            if (Directory.Exists(filePath))
            {
                fileName = filePath + "\\" + symbolFile.StripInvalidPathChars() + ".tck";
            }
            else if (File.Exists(folderOrfile))
            {
                fileName = folderOrfile;
            }
            else
            {
                Directory.CreateDirectory(filePath);
                fileName = filePath + "\\" + symbolFile.StripInvalidPathChars() + ".tck";
                //throw new ApplicationException("Requires either a file or folder to read data. Tried both " + folderOrfile + " and " + filePath);
            }
            CheckFileExtension();
            if (debug) log.DebugFormat(LogMessage.LOGMSG357, fileName);
            try
            {
                OpenFile();
            }
            catch( InvalidOperationException)
            {
                CloseFileForReading();
                throw;
            }
            catch( EndOfStreamException)
            {
                endOfData = true;
                log.Notice("File was empty: " + fileName);
            }
            isInitialized = true;
        }

        public void Initialize(string fileName, BinaryFileMode mode)
        {
            this.mode = mode;
            this.fileName = fileName = Path.GetFullPath(fileName);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            name = fileName;
            InitLogging();
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            CheckFileExtension();
            if (debug) log.DebugFormat(LogMessage.LOGMSG357, fileName);
            try
            {
                OpenFile();
            }
            catch (InvalidOperationException)
            {
                CloseFileForReading();
                // Must a be a legacy format
                throw;
            }
            catch (EndOfStreamException)
            {
                endOfData = true;
                log.Notice("File was empty: " + fileName);
            }
            isInitialized = true;
        }

        private Action<Progress> reportProgressCallback;
        private Progress progress = new Progress();
        private void progressCallback(string text, Int64 current, Int64 final)
        {
            if (ReportProgressCallback != null )
            {
                progress.UpdateProgress(text, current, final);
                ReportProgressCallback(progress);
            }
        }

        private void OpenFile()
        {
            switch (mode)
            {
                case BinaryFileMode.Read:
                    OpenFileForReading();
                    try
                    {
                        ReadNextBlock();
                        endOfData = false;
                    }
                    catch( EndOfStreamException)
                    {
                        throw new EndOfStreamException("File was empty: " + fileName);
                    }
                    break;
                case BinaryFileMode.Write:
                    break;
                default:
                    throw new ApplicationException("Unknown file mode: " + mode);
            }
        }

        private void OpenFileForWriting()
        {
            if (eraseFileToStart)
            {
                log.NoticeFormat("Binary file {0} will be erased to begin writing.", name);
                CreateFileForWriting();
            }
            else
            {
                if( File.Exists(fileName))
                {
                    // Read the file header.
                    try
                    {
                        OpenFileForReading();
                    }
                    finally
                    {
                        CloseFileForReading();
                    }
                    OpenFileForAppending();
                }
                else
                {
                    CreateFileForWriting();
                }
            }
        }

        private void OpenFileForAppending()
        {
            fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read, 1024, FileOptions.WriteThrough);
            log.DebugFormat(LogMessage.LOGMSG358);
        }

        private unsafe void CreateFileForWriting()
        {
            fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read, 1024, FileOptions.WriteThrough);
            log.DebugFormat(LogMessage.LOGMSG358);
            fileHeader.blockHeader.version = 1;
            fileHeader.blockHeader.type = BinaryBlockType.FileHeader;
            fileHeader.blockSize =  1024 * 8;
            fileHeader.utcTimeStamp = Factory.Parallel.UtcNow.Internal;
            fileHeader.SetChecksum();
            var headerBytes = new byte[fileHeader.blockSize];
            fixed( byte *bptr = headerBytes)
            {
                *((BinaryFileHeader*) bptr) = fileHeader;
            }
            fs.Write(headerBytes, 0, fileHeader.blockSize);
            fileBlock = new FileBlock(fileHeader.blockSize);
        }

        void LogInfo(string logMsg)
        {
            if (!quietMode)
            {
                log.Notice(logMsg);
            }
            else
            {
                log.DebugFormat(LogMessage.LOGMSG611, logMsg);
            }
        }

        private string FindFile(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            string[] paths = Directory.GetFiles(directory, name, SearchOption.AllDirectories);
            if (paths.Length == 0)
            {
                return null;
            }
            else if (paths.Length > 1)
            {
                throw new FileNotFoundException("Sorry, found multiple files with name: " + name + " under directory: " + directory);
            }
            else
            {
                return paths[0];
            }
        }

        private FastQueue<FileBlock> streamsToWrite = Factory.Parallel.FastQueue<FileBlock>("BinaryFileDirtyPages");
        private FastQueue<FileBlock> streamsAvailable = Factory.Parallel.FastQueue<FileBlock>("BinaryFileAvailable");

        private void MoveMemoryToQueue()
        {
            using (memoryLocker.Using())
            {
                streamsToWrite.Enqueue(fileBlock, 0L);
            }
            if (streamsAvailable.Count == 0)
            {
                fileBlock = new FileBlock(fileHeader.blockSize);
            }
            else
            {
                using (memoryLocker.Using())
                {
                    streamsAvailable.Dequeue(out fileBlock);
                }
            }
        }

        private void HandleFirstWrite()
        {
            try
            {
                OpenFileForWriting();
                fileBlock.ReserveHeader();
                endOfData = false;
            }
            catch (InvalidOperationException)
            {
                CloseFileForReading();
                throw;
            }
            catch (EndOfStreamException)
            {
                endOfData = true;
                log.Notice("File was empty: " + fileName);
            }
        }

        public bool TryWrite(Serializable serializable, long utcTime)
        {
            if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
            if( isFirstWrite)
            {
                HandleFirstWrite();
                isFirstWrite = false;
            }
            TryCompleteAsyncWrite();
            if (trace) log.TraceFormat(LogMessage.LOGMSG359, serializable);
            if (!fileBlock.TryWrite(serializable,utcTime))
            {
                MoveMemoryToQueue();
                fileBlock.ReserveHeader();
                serializable.ResetCompression();
                if (!fileBlock.TryWrite(serializable,utcTime))
                {
                    throw new InvalidOperationException("After creating new block, write failed.");
                }
                TryCompleteAsyncWrite();
                if( writeFileResult == null)
                {
                    writeFileResult = writeFileAction.BeginInvoke(null, null);
                }
            }
            return true;
        }

        public void Write(Serializable serializable, long utcTime)
        {
            if (!IsInitialized) throw new InvalidOperationException("Please call one of the Initialize() methods first.");
            TryWrite(serializable,utcTime);
        }

        public struct FileBlockHeader
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

        private BinaryFileHeader fileHeader;

        private unsafe void OpenFileForReading()
        {
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    if (!quietMode)
                    {
                        LogInfo("Reading from file: " + fileName);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));

                    fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var headerBytes = new byte[sizeof(BinaryBlockHeader)];
                    var headerSize = sizeof(BinaryBlockHeader);
                    var readBytes = fs.Read(headerBytes, 0, headerSize);
                    if (readBytes != headerSize)
                    {
                        throw new InvalidOperationException("Number of header bytes " + readBytes + " differs from size of the header " + headerSize);
                    }
                    fixed( byte *headerPtr = headerBytes)
                    {
                        fileHeader = *((BinaryFileHeader*) headerPtr);
                    }
                    if (!fileHeader.VerifyChecksum())
                    {
                        throw new InvalidOperationException("Checksum failed for file header.");
                    }

                    // Read the entire header block including all padding.
                    fs.Seek(fileHeader.blockSize, SeekOrigin.Begin);
                    // Verify the version number.
                    switch (fileHeader.blockHeader.version)
                    {
                        case 1:
                            break;
                        default:
                            throw new InvalidOperationException("Unrecognized binary file version " + fileHeader.blockHeader.version);
                    }

                    if (!quietMode || debug)
                    {
                        if (debug) log.DebugFormat(LogMessage.LOGMSG360);
                    }
                    fileBlock = new FileBlock(fileHeader.blockSize);
                    readFileStopwatch = new Stopwatch();
                    readFileStopwatch.Start();
                    break;
                }
                catch( InvalidOperationException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    if (e is CollectionTerminatedException) {
                        log.Warn("Reader queue was terminated.");
                    } else if (e is ThreadAbortException) {
                        //	
                    } else if (e is FileNotFoundException) {
                        log.Error("ERROR: " + e.Message);
                    } else {
                        log.Error("ERROR: " + e);
                        Factory.Parallel.Sleep(1000);
                    }
                }
            }
        }

        private unsafe void ReadNextBlock()
        {
            do
            {
                fileBlock.ReadNextBlock(fs);
                var currentSecond = readFileStopwatch.Elapsed.TotalSeconds;
                if( currentSecond > nextProgressUpdateSecond)
                {
                    progressCallback("Loading file...", fs.Position, fs.Length);
                    nextProgressUpdateSecond = (long) currentSecond + 1;
                }
            } while (fileBlock.LastUtcTimeStamp < startTime.Internal);
        }


        private void CheckFileExtension()
        {
            string locatedFile = FindFile(fileName);
            if (locatedFile == null)
            {
                if( mode == BinaryFileMode.Read)
                {
                    throw new FileNotFoundException("Sorry, unable to find the file: " + fileName);
                }
                log.Info("File was not found. Will create it. " + fileName);
            }
            else
            {
                fileName = locatedFile;
            }
        }

        public void GetLast(Serializable serializable)
        {
            if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
            OpenFileForReading();
            var length = fs.Length;
            if( length % fileHeader.blockSize != 0)
            {
                throw new InvalidOperationException("File size " + length + " isn't not an even multiple of block size " + fileHeader.blockSize);
            }
            fs.Seek(- fileHeader.blockSize, SeekOrigin.End);
            ReadNextBlock();
            while (TryRead(serializable))
            {
                // Read till last in the last block.
            }

        }

        public bool TryRead(Serializable serializable)
        {
            if (!IsInitialized) return false;
            if( recordsCount > MaxCount || endOfData)
            {
                return false;
            }
            try
            {
                if( !fileBlock.TryRead(serializable))
                {
                    ReadNextBlock();
                    if (!fileBlock.TryRead(serializable))
                    {
                        throw new InvalidOperationException("Unable to read the first binary data in a new block.");
                    }
                }
                dataVersion = fileBlock.DataVersion;
                recordsCount++;
                return true;
            }
            catch (EndOfStreamException)
            {
                ReportEndOfData();
                return false;
            }
        }

        private void ReportEndOfData()
        {
            progressCallback("Completed loading file...", fs.Position, fs.Position);
            var elapsed = readFileStopwatch.Elapsed;
            var sb = new StringBuilder();
            if ((long)elapsed.TotalDays > 0)
            {
                sb.Append((long) elapsed.TotalDays + " days, ");
                sb.Append((long) elapsed.Hours + " hours, ");
                sb.Append((long) elapsed.Minutes + " minutes");
            }
            else if ((long)elapsed.TotalHours > 0)
            {
                sb.Append((long) elapsed.TotalHours + " hours, ");
                sb.Append((long) elapsed.Minutes + " minutes");
            }
            else if ((long)elapsed.TotalMinutes > 0)
            {
                sb.Append((long) elapsed.TotalMinutes + " minutes, ");
                sb.Append((long) elapsed.Seconds + " seconds");
            }
            else if ((long)elapsed.TotalSeconds > 0)
            {
                sb.Append((long) elapsed.TotalSeconds + " seconds, ");
                sb.Append((long) elapsed.Milliseconds + " milliseconds");
            }
            else 
            {
                sb.Append((long)elapsed.TotalMilliseconds + " milliseconds");
            }
            log.Notice(recordsCount.ToString("0,0") + " items read for " + name + ". Finished in " + sb);
            endOfData = true;
        }

        private IAsyncResult writeFileResult;

        private void TryCompleteAsyncWrite()
        {
            if (writeFileResult != null && writeFileResult.IsCompleted)
            {
                writeFileAction.EndInvoke(writeFileResult);
                writeFileResult = null;
            }
        }

        public void Flush()
        {
            if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
            while (fileBlock.HasData || streamsToWrite.Count > 0 || writeFileResult != null)
            {
                if (fileBlock.HasData)
                {
                    MoveMemoryToQueue();
                }
                TryCompleteAsyncWrite();
                if (writeFileResult == null)
                {
                    writeFileResult = writeFileAction.BeginInvoke(null, null);
                }
                while (writeFileResult != null)
                {
                    TryCompleteAsyncWrite();
                    Thread.Sleep(100);
                }
            }
        }

        private long lastTimeWritten;

        private long writeCounter = 0;
        private object writeToFileLocker = new object();
        private void WriteToFile()
        {
            if( streamsToWrite.Count == 0) return;
            if( !Monitor.TryEnter(writeToFileLocker))
            {
                throw new InvalidOperationException("Only one thread at a time allowed for this method.");
            }
            try
            {
                while (streamsToWrite.Count > 0)
                {
                    FileBlock fileBlock;
                    using (memoryLocker.Using())
                    {
                        streamsToWrite.Peek(out fileBlock);
                    }
                    if (trace) log.TraceFormat(LogMessage.LOGMSG361, streamsToWrite.Count);
                    fileBlock.Write(fs);
                    lastTimeWritten = fileBlock.LastUtcTimeStamp;
                    using (memoryLocker.Using())
                    {
                        streamsToWrite.Dequeue(out fileBlock);
                        streamsAvailable.Enqueue(fileBlock, 0L);
                    }
                }
            }
            finally
            {
                Monitor.Exit(writeToFileLocker);
            }
        }

        private volatile bool isDisposed = false;
        private object taskLocker = new object();
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
                lock (taskLocker)
                {
                    if (debug) log.DebugFormat(LogMessage.LOGMSG48);

                    if( mode == BinaryFileMode.Write)
                    {
                        CloseFileForWriting();
                    }

                    if (mode == BinaryFileMode.Read)
                    {
                        CloseFileForReading();
                    }

                    if (debug) log.DebugFormat(LogMessage.LOGMSG362);
                    if( lastTimeWritten > 0)
                    {
                        log.Notice("Last time written for " + name + ": " + new TimeStamp(lastTimeWritten));
                    }
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FlushFileBuffers(SafeFileHandle hFile);

        private void CloseFileForReading()
        {
            if (fs != null)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG363);
                fs.Close();
                fs = null;
                log.Info("Closed file " + fileName);
            }
        }

        private void CloseFileForWriting()
        {
            if (fs != null)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG364, fs.Length);
                Flush();
                fs.Flush();
                if (!FlushFileBuffers(fs.SafeFileHandle))   // Flush OS file cache to disk.
                {
                    Int32 err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, "Win32 FlushFileBuffers returned error for " + fs.Name);
                }
                fs.Close();
                fs = null;
                log.Info("Flushed and closed file " + fileName);
            }
        }

        public static void VerifyClosed()
        {
            lock( locker)
            {
                foreach( var file in binaryFiles)
                {
                    file.VerifyClosedInternal();
                }
                binaryFiles.Clear();
            }
        }

        public long VerifyClosedInternal()
        {
            var result = 0L;
            if( !isDisposed)
            {
                var message = "BinaryFile " + fileName + " was never disposed.\n" + constructorTrace;
                if (log == null) log = Factory.SysLog.GetLogger(typeof (BinaryFileBlocked));
                log.Error(message);
                throw new ApplicationException(message);
            }
            if (fs != null)
            {
                try
                {
                    result = fs.Length;
                    var message = "BinaryFile " + fileName + " is still open.\n" + constructorTrace;
                    if (log == null) log = Factory.SysLog.GetLogger(typeof(BinaryFileBlocked));
                    log.Error(message);
                    throw new ApplicationException(message);
                }
                catch
                {

                }
            }
            return result;
        }

        public long Length
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return fs.Length;
            }
        }

        public long Position
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return fs.Position;
            }
        }

        public int DataVersion
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return dataVersion;
            }
        }

        public int BlockVersion
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return fileHeader.blockHeader.version;
            }
        }

        public bool QuietMode
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return quietMode;
            }
            set
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                quietMode = value;
            }
        }

        public string FileName
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return fileName;
            }
        }

        public string Name
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return name;
            }
        }

        public bool EraseFileToStart
        {
            get
            {
                return eraseFileToStart;
            }
            set
            {
                if (IsInitialized) throw new InvalidStateException("Please set EraseFileToStart before any Initialize() method.");
                eraseFileToStart = value;
            }
        }

        public long WriteCounter
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return writeCounter;
            }
        }

        public long MaxCount
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return maxCount;
            }
            set
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                maxCount = value;
            }
        }

        public long StartCount
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return startCount;
            }
            set
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                startCount = value;
            }
        }

        public TimeStamp StartTime
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return startTime;
            }
            set
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                startTime = value;
            }
        }

        public TimeStamp EndTime
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                return endTime;
            }
            set
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                endTime = value;
            }
        }

        public Action<Progress> ReportProgressCallback
        {
            get { return reportProgressCallback; }
            set { reportProgressCallback = value; }
        }

        public bool IsInitialized
        {
            get { return isInitialized; }
        }
    }
}