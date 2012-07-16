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
    public class TickFileBlocked : TickFile
    {
        private TickFileLegacy legacy = new TickFileLegacy();
        private bool isLegacy;
        private SymbolInfo symbol;
        private long lSymbol;
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
        private long tickCount;
        private long maxCount = long.MaxValue;
        private TimeStamp startTime = TimeStamp.MinValue;
        private TimeStamp endTime = TimeStamp.MaxValue;
        private bool endOfData = true;
        private FileBlock fileBlock;
        private long startCount;
        private bool isInitialized;
        private StackTrace constructorTrace;
        private bool isFirstTick = true;
        private Stopwatch readFileStopwatch;
        private long nextProgressUpdateSecond;
        private static object tickFilesLocker = new object();
        private static List<TickFileBlocked> tickFiles = new List<TickFileBlocked>();

        public unsafe TickFileBlocked()
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
            lock( tickFilesLocker)
            {
                tickFiles.Add(this);
            }
            constructorTrace = new StackTrace(true);
        }

        private void InitLogging()
        {
            log = Factory.SysLog.GetLogger("TickZoom.TickUtil.TickFileBlocked." + mode + "." + symbol.ExpandedSymbol.StripInvalidPathChars());
            debug = log.IsDebugEnabled;
            trace = log.IsTraceEnabled;
        }

        public void Initialize(string folderOrfile, string symbolFile, BinaryFileMode mode)
        {
            string[] symbolParts = symbolFile.Split(new char[] { '.' });
            string _symbol = symbolParts[0];
            this.mode = mode;
            symbol = Factory.Symbol.LookupSymbol(_symbol);
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
                // Must a be a legacy format
                isLegacy = true;
                legacy.Initialize(folderOrfile,symbolFile,mode);
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
            if (symbol == null)
            {
                symbol = Factory.Symbol.LookupSymbol(baseName.Replace("_Tick", ""));
                lSymbol = symbol.BinaryIdentifier;
            }
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
                isLegacy = true;
                legacy.Initialize(fileName, mode);
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
            lSymbol = symbol.BinaryIdentifier;
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
                log.Notice("TickWriter file will be erased to begin writing.");
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

        private FastQueue<FileBlock> streamsToWrite = Factory.Parallel.FastQueue<FileBlock>("TickFileDirtyPages");
        private FastQueue<FileBlock> streamsAvailable = Factory.Parallel.FastQueue<FileBlock>("TickFileAvailable");

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
                // Must a be a legacy format
                isLegacy = true;
                legacy.Initialize(fileName, mode);
            }
            catch (EndOfStreamException)
            {
                endOfData = true;
                log.Notice("File was empty: " + fileName);
            }
        }

        public bool TryWriteTick(TickIO tickIO)
        {
            if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
            if( isFirstTick)
            {
                HandleFirstWrite();
                isFirstTick = false;
            }
            if (isLegacy) return legacy.TryWriteTick(tickIO);
            TryCompleteAsyncWrite();
            if (trace) log.TraceFormat(LogMessage.LOGMSG359, tickIO);
            if( !fileBlock.TryWrite(tickIO, tickIO.lUtcTime))
            {
                MoveMemoryToQueue();
                fileBlock.ReserveHeader();
                tickIO.ResetCompression();
                if (!fileBlock.TryWrite(tickIO,tickIO.lUtcTime))
                {
                    throw new InvalidOperationException("After creating new block, writing tick failed.");
                }
                TryCompleteAsyncWrite();
                if( writeFileResult == null)
                {
                    writeFileResult = writeFileAction.BeginInvoke(null, null);
                }
            }
            return true;
        }

        public void WriteTick(TickIO tickIO)
        {
            if (!IsInitialized) throw new InvalidOperationException("Please call one of the Initialize() methods first.");
            TryWriteTick(tickIO);
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
                    var headerBytes = new byte[sizeof(BinaryFileHeader)];
                    var headerSize = sizeof (BinaryFileHeader);
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
                            throw new InvalidOperationException("Unrecognized tick file version " + fileHeader.blockHeader.version);
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

        private unsafe void JumpToLast()
        {
            fileBlock.JumpToLast(fs);
        }

        private unsafe void ReadNextBlock()
        {
            var skipCount = 0L;
            do
            {
                fileBlock.ReadNextBlock(fs);
                var currentSecond = readFileStopwatch.Elapsed.TotalSeconds;
                if( currentSecond > nextProgressUpdateSecond)
                {
                    progressCallback("Loading file...", fs.Position, fs.Length);
                    nextProgressUpdateSecond = (long) currentSecond + 1;
                }
                skipCount++;
            } while (fileBlock.LastUtcTimeStamp < startTime.Internal);
            if( skipCount > 1)
            {
                if( debug) log.DebugFormat("Skipped {0} ticks to find start tick.", skipCount);
            }
        }


        private void CheckFileExtension()
        {
            string locatedFile = FindFile(fileName);
            if (locatedFile == null)
            {
                if (fileName.Contains("_Tick.tck"))
                {
                    locatedFile = FindFile(fileName.Replace("_Tick.tck", ".tck"));
                }
                else
                {
                    locatedFile = FindFile(fileName.Replace(".tck", "_Tick.tck"));
                }
                if (locatedFile != null)
                {
                    fileName = locatedFile;
                    log.Warn("Deprecated: Please use new style .tck file names by removing \"_Tick\" from the name.");
                }
                else if( mode == BinaryFileMode.Read)
                {
                    throw new FileNotFoundException("Sorry, unable to find the file: " + fileName);
                }
                else
                {
                    log.Info("File was not found. Will create it. " + fileName);
                }
            }
            else
            {
                fileName = locatedFile;
            }
        }

        public void GetLastTick(TickIO lastTickIO)
        {
            if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
            if (isLegacy)
            {
                legacy.GetLastTick(lastTickIO);
                return; 
            }
            OpenFileForReading();
            var length = fs.Length;
            if( length % fileHeader.blockSize != 0)
            {
                throw new InvalidOperationException("File size " + length + " isn't not an even multiple of block size " + fileHeader.blockSize);
            }
            fs.Seek(- fileHeader.blockSize, SeekOrigin.End);
            JumpToLast();
            ReadNextBlock();
            var skipCount = 0L;
            while( TryReadTick(lastTickIO))
            {
                skipCount++;
                // Read till last tick in the last block.
            }
            if( debug) log.DebugFormat("Skipped {0} ticks to find last tick.", skipCount);
        }

        public bool TryReadTick(TickIO tickIO)
        {
            if (!IsInitialized) return false;
            if (isLegacy) return legacy.TryReadTick(tickIO);
            if( tickCount > MaxCount || endOfData)
            {
                return false;
            }
            try
            {
                var skipCount = 0L;
                do
                {
                    tickIO.SetSymbol(lSymbol);
                    if (!fileBlock.TryRead(tickIO))
                    {
                        ReadNextBlock();
                        if (!fileBlock.TryRead(tickIO))
                        {
                            throw new InvalidOperationException("Unable to write the first tick in a new block.");
                        }
                    }
                    var utcTime = new TimeStamp(tickIO.lUtcTime);
                    tickIO.SetTime(utcTime);
                    dataVersion = fileBlock.DataVersion;
                    if (tickIO.lUtcTime > EndTime.Internal)
                    {
                        ReportEndOfData();
                        return false;
                    }
                    tickCount++;
                } while (tickIO.UtcTime < StartTime);
                if( skipCount > 1)
                {
                    if( debug) log.DebugFormat("Skipped {0} ticks to find start tick.", skipCount);
                }
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
            log.Notice(tickCount.ToString("0,0") + " ticks read for " + symbol + ". Finished in " + sb);
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
            if (isLegacy)
            {
                legacy.Flush();
                return;
            }
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
                    var sleepMs = 100;
                    if( debug) log.DebugFormat("Sleeping {0}ms till write result is good", sleepMs);
                    Thread.Sleep(sleepMs);
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
            if( isLegacy)
            {
                legacy.Dispose();
            }
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
                        log.Notice("Last tick written for " + symbol + ": " + new TimeStamp(lastTimeWritten));
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
            lock( tickFilesLocker)
            {
                foreach( var file in tickFiles)
                {
                    file.VerifyClosedInternal();
                }
                tickFiles.Clear();
            }
        }

        public long VerifyClosedInternal()
        {
            var result = 0L;
            if( !isDisposed)
            {
                var message = "TickFile " + fileName + " was never disposed.\n" + constructorTrace;
                if (log == null) log = Factory.SysLog.GetLogger(typeof (TickFileBlocked));
                log.Error(message);
                throw new ApplicationException(message);
            }
            if (fs != null)
            {
                try
                {
                    result = fs.Length;
                    var message = "TickFile " + fileName + " is still open.\n" + constructorTrace;
                    if (log == null) log = Factory.SysLog.GetLogger(typeof(TickFileBlocked));
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
                if (isLegacy) return legacy.Length;
                return fs.Length;
            }
        }

        public long Position
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy) return legacy.Position;
                return fs.Position;
            }
        }

        public int DataVersion
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy) return legacy.DataVersion;
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
                if (isLegacy) return legacy.QuietMode;
                return quietMode;
            }
            set
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy)
                {
                    legacy.QuietMode = value;
                    return;
                }
                quietMode = value;
            }
        }

        public string FileName
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy) return legacy.FileName;
                return fileName;
            }
        }

        public SymbolInfo Symbol
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy) return legacy.Symbol;
                return symbol;
            }
        }

        public bool EraseFileToStart
        {
            get
            {
                if (isLegacy) return legacy.EraseFileToStart;
                return eraseFileToStart;
            }
            set
            {
                if (IsInitialized) throw new InvalidStateException("Please set EraseFileToStart before any Initialize() method.");
                legacy.EraseFileToStart = value;
                eraseFileToStart = value;
            }
        }

        public long WriteCounter
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy) return legacy.WriteCounter;
                return writeCounter;
            }
        }

        public long MaxCount
        {
            get
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy) return legacy.MaxCount;
                return maxCount;
            }
            set
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy)
                {
                    legacy.MaxCount = value;
                    return;
                }
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
                if (isLegacy) return legacy.StartTime;
                return startTime;
            }
            set
            {
                if (!IsInitialized) throw new InvalidStateException("Please call one of the Initialize() methods first.");
                if (isLegacy)
                {
                    legacy.StartTime = value;
                }
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
                if (isLegacy)
                {
                    legacy.EndTime = value;
                }
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