#region Copyright
/*
 * Software: TickZoom Trading Platform
 * Copyright 2009 M. Wayne Walter
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 * 
 * Business use restricted to 30 days except as otherwise stated in
 * in your Service Level Agreement (SLA).
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <http://www.tickzoom.org/wiki/Licenses>
 * or write to Free Software Foundation, Inc., 51 Franklin Street,
 * Fifth Floor, Boston, MA  02110-1301, USA.
 * 
 */
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using TickZoom.Api;

namespace TickZoom.TickUtil
{
	public abstract class Reader
	{
		BackgroundWorker backgroundWorker;
		long maxCount = long.MaxValue;
		SymbolInfo symbol = null;
		long lSymbol = 0;
		static readonly Log log = Factory.SysLog.GetLogger("TickZoom.TickUtil.Reader");
		readonly bool debug = log.IsDebugEnabled;
		readonly bool trace = log.IsDebugEnabled;
		bool quietMode = false;
		long progressDivisor = 1;
		private Elapsed sessionStart = new Elapsed(8, 0, 0);
		private Elapsed sessionEnd = new Elapsed(12, 0, 0);
		bool excludeSunday = true;
		string fileName = null;
		bool logProgress = false;
		bool bulkFileLoad = false;
		private Receiver receiver;
		protected Task fileReaderTask;
		private static object readerListLocker = new object();
		private static List<Reader> readerList = new List<Reader>();
		string priceDataFolder;
		string appDataFolder;
		MemoryStream memory;
		byte[] buffer;
		Progress progress = new Progress();
		private Pool<TickBinaryBox> tickBoxPool;
		private TickBinaryBox box;
	    private int diagnoseMetric;
	    private ReceiveEventQueue outboundQueue;

		public Reader()
		{
			var property = "PriceDataFolder";
			priceDataFolder = Factory.Settings[property];
			if (priceDataFolder == null) {
				throw new ApplicationException("Must set " + property + " property in app.config");
			}
			property = "AppDataFolder";
			appDataFolder = Factory.Settings[property];
			if (appDataFolder == null) {
				throw new ApplicationException("Must set " + property + " property in app.config");
			}
			lock(readerListLocker) {
				readerList.Add(this);
			}
			memory = new MemoryStream();
			memory.SetLength(TickImpl.minTickSize);
			buffer = memory.GetBuffer();
			TickEventMethod = TickEvent;
			SendFinishMethod = SendFinish;
			StartEventMethod = StartEvent;
		}

		bool CancelPending {
			get { return backgroundWorker != null && backgroundWorker.CancellationPending; }
		}

		[Obsolete("Pass symbol string instead of SymbolInfo", true)]
		public void Initialize(string _folder, SymbolInfo symbolInfo)
		{
			throw new NotImplementedException("Please use the Initialize() method with a string for the symbol which gets used as part of the file name.");
		}

		public void Initialize(string folderOrfile, string symbolFile)
		{
			string[] symbolParts = symbolFile.Split(new char[] { '.' });
			string _symbol = symbolParts[0];
			symbol = Factory.Symbol.LookupSymbol(_symbol);
			lSymbol = symbol.BinaryIdentifier;
			var dataFolder = folderOrfile.Contains(@"Test\") ? appDataFolder : priceDataFolder;
			var filePath = dataFolder + "\\" + folderOrfile;
			if (Directory.Exists(filePath)) {
				fileName = filePath + "\\" + symbolFile.StripInvalidPathChars() + ".tck";
			} else if (File.Exists(folderOrfile)) {
				fileName = folderOrfile;
			} else {
				throw new ApplicationException("Requires either a file or folder to read data. Tried both " + folderOrfile + " and " + filePath);
			}
			CheckFileExtension();
			PrepareTask();
		}

		private string FindFile(string path)
		{
			string directory = Path.GetDirectoryName(path);
			string name = Path.GetFileName(path);
			string[] paths = Directory.GetFiles(directory, name, SearchOption.AllDirectories);
			if (paths.Length == 0) {
				return null;
			} else if (paths.Length > 1) {
				throw new FileNotFoundException("Sorry, found multiple files with name: " + name + " under directory: " + directory);
			} else {
				return paths[0];
			}
		}

		private void CheckFileExtension()
		{
			string locatedFile = FindFile(fileName);
			if (locatedFile == null) {
				if (fileName.Contains("_Tick.tck")) {
					locatedFile = FindFile(fileName.Replace("_Tick.tck", ".tck"));
				} else {
					locatedFile = FindFile(fileName.Replace(".tck", "_Tick.tck"));
				}
				if (locatedFile != null) {
					fileName = locatedFile;
					log.Warn("Deprecated: Please use new style .tck file names by removing \"_Tick\" from the name.");
				} else {
					throw new FileNotFoundException("Sorry, unable to find the file: " + fileName);
				}
			}
			fileName = locatedFile;
		}

		public void Initialize(string fileName)
		{
            this.fileName = fileName = Path.GetFullPath(fileName);
			CheckFileExtension();
			if (debug)
				log.Debug("File Name = " + fileName);
			if (debug)
				log.Debug("Setting start method on reader queue.");
			string baseName = Path.GetFileNameWithoutExtension(fileName);
			if (symbol == null) {
				symbol = Factory.Symbol.LookupSymbol(baseName.Replace("_Tick", ""));
				lSymbol = symbol.BinaryIdentifier;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(fileName));
			PrepareTask();
		}

		public TickIO GetLastTick()
		{
			Stream stream;
			stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			dataIn = new BinaryReader(stream, Encoding.Unicode);
			TickImpl lastTickIO = new TickImpl();
			int count = 0;
			try {
				while (stream.Position < stream.Length && !CancelPending) {
					long lastPosition = stream.Position;
					if (!TryReadTick()) {
						break;
					}
					count ++;
					lastTickIO.Inject(tickIO.Extract());
				}
			} catch (ObjectDisposedException) {
				// Only partial tick was read at the end of the file.
				// Another writer must not have completed.
				log.Warn("ObjectDisposedException returned from tickIO.FromReader(). Incomplete last tick. Ignoring.");
			} catch {
				log.Error("Error reading tick #" + count);
			}
			isTaskPrepared = false;
			return lastTickIO;
		}

        private SimpleLock startLocker = new SimpleLock();
		public virtual void Start(Receiver receiver)
		{
            using (startLocker.Using())
            {
                if (!isTaskPrepared)
                {
    				throw new ApplicationException("Read must be Initialized before Start() can be called and after GetLastTick() the reader must be disposed.");
	    		}
                if (!isStarted)
                {
                    this.receiver = receiver;
                    this.outboundQueue = receiver.GetQueue(symbol);
                    if (debug)
                        log.Debug("Start called.");
                    start = Factory.TickCount;
                    diagnoseMetric = Diagnose.RegisterMetric("Reader-" + symbol);
                    fileReaderTask = Factory.Parallel.IOLoop(this, OnException, FileReader);
                    fileReaderTask.Scheduler = Scheduler.RoundRobin;
                    fileReaderTask.Start();
                    isStarted = true;
                }
            }
		}

		private void OnException(Exception ex)
		{
			ErrorDetail detail = new ErrorDetail();
			detail.ErrorMessage = ex.ToString();
            log.Error(detail.ErrorMessage);
		}

		public void Stop(Receiver receiver)
		{
			if (debug)
				log.Debug("Stop(" + receiver + ")");
			Dispose();
		}

		public abstract bool IsAtStart(TickBinary tick);
		public abstract bool IsAtEnd(TickBinary tick);

		public bool LogTicks = false;

		void LogInfo(string logMsg)
		{
			if (!quietMode) {
				log.Notice(logMsg);
			} else {
				log.Debug(logMsg);
			}
		}
		static Dictionary<SymbolInfo, byte[]> fileBytesDict = new Dictionary<SymbolInfo, byte[]>();
		static object fileLocker = new object();
		BinaryReader dataIn = null;
		TickImpl tickIO = new TickImpl();
		long lastTime = 0;

		TickBinary tick = new TickBinary();
		bool isDataRead = false;
		bool isFirstTick = true;
		long nextUpdate = 0;
		int count = 0;
		protected volatile int tickCount = 0;
		long start;
		bool isTaskPrepared = false;
		bool isStarted = false;
		
		private bool PrepareTask()
		{
		    tickBoxPool = Factory.TickUtil.TickPool(symbol);
            for (int retry = 0; retry < 3; retry++)
            {
				try {
					if (!quietMode) {
						LogInfo("Reading from file: " + fileName);
					}

					Directory.CreateDirectory(Path.GetDirectoryName(fileName));

					Stream stream;
					if (bulkFileLoad) {
						byte[] filebytes;
						lock (fileLocker) {
							if (!fileBytesDict.TryGetValue(symbol, out filebytes)) {
								filebytes = File.ReadAllBytes(fileName);
								fileBytesDict[symbol] = filebytes;
							}
						}
						stream = new MemoryStream(filebytes);
					} else {
                        var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					    stream = new BufferedStream(fileStream, 15*1024);
					}

					dataIn = new BinaryReader(stream, Encoding.Unicode);

					progressDivisor = dataIn.BaseStream.Length / 20;
					if (!quietMode || debug) {
						if (debug)
							log.Debug("Starting to read data.");
						log.Indent();
					}
					progressCallback("Loading bytes...", dataIn.BaseStream.Position, dataIn.BaseStream.Length);
					isTaskPrepared = true;
					return true;
				} catch (Exception ex) {
					ExceptionHandler(ex);
				}
				Factory.Parallel.Sleep(1000);
			}
			return false;
		}

		private void ExceptionHandler(Exception e)
		{
			if (e is CollectionTerminatedException) {
				log.Warn("Reader queue was terminated.");
			} else if (e is ThreadAbortException) {
				//	
			} else if (e is FileNotFoundException) {
				log.Error("ERROR: " + e.Message);
			} else {
				log.Error("ERROR: " + e);
			}
			if (dataIn != null) {
				isDisposed = true;
				dataIn.Close();
				dataIn = null;
			}
		}

		int dataVersion;

		public int DataVersion {
			get { return dataVersion; }
		}

		private bool TryReadTick()
		{
            try
            {
                tickIO.SetSymbol(lSymbol);
                byte size = dataIn.ReadByte();
                // Check for old style prior to version 8 where
                // single byte version # was first.
                if (dataVersion < 8 && size < 8)
                {
                    dataVersion = tickIO.FromReader((byte) size, dataIn);
                }
                else
                {
                    // Subtract the size byte.
                    //if (dataIn.BaseStream.Position + size - 1 > length) {
                    //    return false;
                    //}
                    int count = 1;
                    memory.SetLength(size);
                    memory.GetBuffer()[0] = size;
                    while (count < size)
                    {
                        var bytesRead = dataIn.Read(buffer, count, size - count);
                        if( bytesRead == 0)
                        {
                            return false;
                        }
                        count += bytesRead;
                    }
                    memory.Position = 0;
                    dataVersion = tickIO.FromReader(memory);
                }
                var utcTime = new TimeStamp(tickIO.lUtcTime);
                tickIO.SetTime(utcTime);
                return true;
            } catch( EndOfStreamException) {
                return false;
            }
		}
		
		private YieldMethod TickEventMethod;
		private YieldMethod SendFinishMethod;
		private YieldMethod StartEventMethod;

		private Yield FileReader()
		{
			lock (taskLocker) {
				if (isDisposed)
					return Yield.Terminate;
				long position = dataIn.BaseStream.Position;
				try {
					if (!CancelPending && TryReadTick()) {

						tick = tickIO.Extract();
						isDataRead = true;

						if (Factory.TickCount > nextUpdate) {
							try {
								progressCallback("Loading bytes...", dataIn.BaseStream.Position, dataIn.BaseStream.Length);
							} catch (Exception ex) {
								log.Debug("Exception on progressCallback: " + ex.Message);
							}
							nextUpdate = Factory.TickCount + 2000;
						}

						if (maxCount > 0 && count > maxCount) {
							if (debug)
								log.Debug("Ending data read because count reached " + maxCount + " ticks.");
							return Yield.DidWork.Invoke(SendFinishMethod);
						}

						if (IsAtEnd(tick)) {
							return Yield.DidWork.Invoke(SendFinishMethod);
						}

						if (IsAtStart(tick)) {
							count++;
							if (debug && count < 10) {
								log.Debug("Read a tick " + tickIO);
							} else if (trace) {
								log.Trace("Read a tick " + tickIO);
							}
							tick.Symbol = symbol.BinaryIdentifier;

							if (tick.UtcTime <= lastTime) {
								tick.UtcTime = lastTime + 1;
							}
							lastTime = tick.UtcTime;

							if (isFirstTick) {
								isFirstTick = false;
								return Yield.DidWork.Invoke(StartEventMethod);
							} else {
								tickCount++;
							}
							
							box = tickBoxPool.Create();
						    var tickId = box.TickBinary.Id;
							box.TickBinary = tick;
						    box.TickBinary.Id = tickId;

							return Yield.DidWork.Invoke(TickEventMethod);
						}
						tickCount++;

					} else {
                        if( debug) log.Debug("Finished reading to file length: " + dataIn.BaseStream.Length);
						return Yield.DidWork.Invoke(SendFinishMethod);
					}
				} catch (ObjectDisposedException) {
					return Yield.DidWork.Invoke(SendFinishMethod);
				}
				return Yield.DidWork.Repeat;
			}
		}

        private Yield StartEvent()
		{
		    var item = new EventItem
		                   {
		                       Symbol = symbol,
		                       EventType = (int) EventType.StartHistorical,
		                   };
            if (!outboundQueue.TryEnqueue(item,TimeStamp.UtcNow.Internal))
            {
				throw new ApplicationException( "Enqueue failed for " + outboundQueue.Name);
			} else {
				if (!quietMode) {
					LogInfo("Starting loading for " + symbol + " from " + tickIO.ToPosition());
				}
				box = tickBoxPool.Create();
			    var tickId = box.TickBinary.Id;
				box.TickBinary = tick;
			    box.TickBinary.Id = tickId;

				return Yield.DidWork.Invoke(TickEventMethod);
			}
		}

		private Yield TickEvent()
		{
			if( box == null) {
				throw new ApplicationException("Box is null.");
			}
            try
            {
    		    var item = new EventItem
		                   {
		                       Symbol = symbol,
		                       EventType = (int)EventType.Tick,
                               EventDetail = box
		                   };
                if (!outboundQueue.TryEnqueue(item,box.TickBinary.UtcTime))
                {
				    throw new ApplicationException( "Enqueue failed for " + outboundQueue.Name);
                }
                else
                {
                    if (Diagnose.TraceTicks) Diagnose.AddTick(diagnoseMetric, ref box.TickBinary);
                    box = null;
                    return Yield.DidWork.Return;
                }
            } catch( QueueException)
            {
                // receiver terminated.
                return Yield.DidWork.Invoke(FinishTask);
            }
		}

		private Yield SendFinish()
		{
		    var item = new EventItem
		                   {
		                       Symbol = symbol,
		                       EventType = (int)EventType.EndHistorical,
		                   };
            if (!outboundQueue.TryEnqueue(item,TimeStamp.UtcNow.Internal))
            {
				throw new ApplicationException( "Enqueue failed for " + outboundQueue.Name);
			} else {
				if( debug) log.Debug("EndHistorical for " + symbol);
				return Yield.DidWork.Invoke(FinishTask);
			}
		}

		private Yield FinishTask()
		{
			try {
				if (!quietMode && isDataRead) {
					LogInfo("Processing ended for " + symbol + " at " + tickIO.ToPosition());
				}
				long end = Factory.TickCount;
				if (!quietMode) {
					LogInfo("Processed " + count + " ticks in " + (end - start) + " ms.");
				}
				try {
					progressCallback("Processing complete.", dataIn.BaseStream.Length, dataIn.BaseStream.Length);
				} catch (Exception ex) {
					log.Debug("Exception on progressCallback: " + ex.Message);
				}
				if (debug)
					log.Debug("calling receiver.OnEvent(symbol,(int)EventType.EndHistorical)");
			} catch (ThreadAbortException) {

			} catch (FileNotFoundException ex) {
				log.Error("ERROR: " + ex.Message);
			} catch (Exception ex) {
				log.Error("ERROR: " + ex);
			} finally {
				isDisposed = true;
				fileReaderTask.Stop();
				if (dataIn != null) {
					dataIn.Close();
				}
			}
			return Yield.Terminate;
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
			if (!isDisposed) {
				isDisposed = true;
				lock (taskLocker) {
					if (fileReaderTask != null) {
						fileReaderTask.Stop();
						fileReaderTask.Join();
					}
					if (dataIn != null) {
						if( dataIn.BaseStream != null) {
							dataIn.BaseStream.Close();
						}
						dataIn.Close();
					}
					lock( readerListLocker) {
						readerList.Remove(this);
					}
				}
			}
		}

		public void CloseAll()
		{
			lock( readerListLocker) {
				for (int i = 0; i < readerList.Count; i++) {
					readerList[i].Dispose();
				}
				readerList.Clear();
			}
		}

		void progressCallback(string text, Int64 current, Int64 final)
		{
			if (backgroundWorker != null && !backgroundWorker.CancellationPending && backgroundWorker.WorkerReportsProgress) {
				progress.UpdateProgress(text, current, final);
				backgroundWorker.ReportProgress(0, progress);
			}
		}

		public BackgroundWorker BackgroundWorker {
			get { return backgroundWorker; }
			set { backgroundWorker = value; }
		}

		public Elapsed SessionStart {
			get { return sessionStart; }
			set { sessionStart = value; }
		}

		public Elapsed SessionEnd {
			get { return sessionEnd; }
			set { sessionEnd = value; }
		}

		public bool ExcludeSunday {
			get { return excludeSunday; }
			set { excludeSunday = value; }
		}

		public string FileName {
			get { return fileName; }
		}

		public SymbolInfo Symbol {
			get { return symbol; }
		}

		public bool LogProgress {
			get { return logProgress; }
			set { logProgress = value; }
		}

		public long MaxCount {
			get { return maxCount; }
			set { maxCount = value; }
		}

		public bool QuietMode {
			get { return quietMode; }
			set { quietMode = value; }
		}

		public bool BulkFileLoad {
			get { return bulkFileLoad; }
			set { bulkFileLoad = value; }
		}

		public TickIO LastTick {
			get { return tickIO; }
		}
	}
}
