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
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using TickZoom.Api;

namespace TickZoom.TickUtil
{

	/// <summary>
	/// Description of TickArray.
	/// </summary>
	public class TickWriterDefault : TickWriter
	{
		private BackgroundWorker backgroundWorker;
   		private int maxCount = 0;
   		private SymbolInfo symbol = null;
		private string fileName = null;
		private Task appendTask = null;
		protected TickQueue writeQueue;
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(TickWriterDefault));
		private readonly bool debug = log.IsDebugEnabled;
		private readonly bool trace = log.IsTraceEnabled;
		private bool keepFileOpen = true;
		private bool eraseFileToStart = false;
		private bool logProgress = false;
		private FileStream fs = null;
        private TaskLock memoryLocker = new TaskLock();
		private MemoryStream memory = null;
		private bool isInitialized = false;
		private string priceDataFolder;
		private string appDataFolder;
		private Progress progress = new Progress();
		private Action writeFileAction;
		private IAsyncResult writeFileResult;
		
		public TickWriterDefault(bool eraseFileToStart)
		{
			this.eraseFileToStart = eraseFileToStart;
			writeQueue = Factory.TickUtil.TickQueue(typeof(TickWriter));
			writeQueue.StartEnqueue = Start;
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
			writeFileAction = WriteToFile;
		}
		
		public void Start() {
			
		}
		
		public void Pause() {
			log.Notice("Disk I/O for " + symbol + " is temporarily paused.");
			appendTask.Pause();
		}
		
		public void Resume() {
			log.Notice("Disk I/O for " + symbol + " has resumed.");
		}
		
		bool CancelPending {
			get { return backgroundWorker !=null && backgroundWorker.CancellationPending; }
		}
		
		public void Initialize(string folderOrfile, string _symbol) {
			SymbolInfo symbolInfo = Factory.Symbol.LookupSymbol(_symbol);
			
			var dataFolder = folderOrfile.Contains(@"Test\") ? appDataFolder : priceDataFolder;
			
			symbol = Factory.Symbol.LookupSymbol(_symbol);
			if( string.IsNullOrEmpty(Path.GetExtension(folderOrfile))) {
				fileName = dataFolder + Path.DirectorySeparatorChar + folderOrfile + Path.DirectorySeparatorChar + symbol.Symbol.StripInvalidPathChars() + ".tck";
			} else {
    			fileName = folderOrfile;
			}
			          
    		log.Notice("TickWriter fileName: " + fileName);
    		var path = Path.GetFullPath(fileName);
		    path = Path.GetDirectoryName(path);
    		if( path != null) {
    			Directory.CreateDirectory( path);
    		}
			if( eraseFileToStart) {
    			File.Delete( fileName);
    			log.Notice("TickWriter file was erased to begin writing.");
    		}
			if( keepFileOpen) {
    			fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read, 1024, FileOptions.WriteThrough);
   				log.Debug("keepFileOpen - Open()");
    			memory = new MemoryStream();
			}
     		if( !CancelPending ) {
				StartAppendThread();
			}
			isInitialized = true;
		}

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [Obsolete("Please pass string symbol instead of SymbolInfo.", true)]
		public void Initialize(string _folder, SymbolInfo _symbol) {
			isInitialized = false;
		}
		
		[Obsolete("Please call Initialize( folderOrfile, symbol) instead.",true)]
		public void Initialize(string filePath) {
			isInitialized = false;
		}
		
		private void OnException( Exception ex) {
			log.Error( ex.Message, ex);
		}

		protected virtual void StartAppendThread() {
			string baseName = Path.GetFileNameWithoutExtension(fileName);
			appendTask = Factory.Parallel.Loop(baseName + " writer",OnException, AppendData);
			appendTask.Scheduler = Scheduler.EarliestTime;
			writeQueue.ConnectInbound(appendTask);
			appendTask.Start();
		}
		
		TickBinary tick = new TickBinary();
		TickIO tickIO = new TickImpl();

        private object asyncWriteLocker = new object();

        private bool CompleteAsyncWrite()
        {
            var result = false;
            lock( asyncWriteLocker)
            {
                if (writeFileResult == null)
                {
                    result = true;
                } else if (writeFileResult.IsCompleted)
                {
                    writeFileAction.EndInvoke(writeFileResult);
                    writeFileResult = null;
                    result = true;
                }
            }
            return result;
        }

	    private long appendCounter = 0;
		
		protected virtual Yield AppendData() {
			var result = Yield.NoWork.Repeat;
			try {
				if( writeQueue.Count == 0) {
					return result;
				}
				if( !keepFileOpen) {
	    			fs = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read, 1024, FileOptions.WriteThrough);
	    			if( trace) log.Trace("!keepFileOpen - Open()");
	    			memory = new MemoryStream();
				}
				while( writeQueue.Count > 0) {
                    if( !CompleteAsyncWrite())
                    {
                        break;
                    }
                    if (!memoryLocker.TryLock())
                    {
                        break;
                    }
                    try
                    {
                        if (!writeQueue.TryDequeue(ref tick))
                        {
    						break;
	    				}
			    		tickIO.Inject(tick);
				    	if( trace)  log.Trace("Writing to file: " + tickIO);
                        tickIO.ToWriter(memory);
                    }
                    finally
                    {
                        memoryLocker.Unlock();
                    }
                    if (memory.Position > 5000)
                    {
                        lock (asyncWriteLocker)
                        {
                            writeFileResult = writeFileAction.BeginInvoke(null, null);
                        }
                    }
                    result = Yield.DidWork.Repeat;
				}
				if( !keepFileOpen)
				{
                    CloseFile(fs);
                    fs = null;
                    if (trace) log.Trace("!keepFileOpen - Close()");
		    	}
	    		return result;
		    } catch (QueueException ex) {
				if( ex.EntryType == EventType.Terminate) {
                    log.Notice("Last tick written: " + tickIO);
                    if( debug) log.Debug("Exiting, queue terminated.");
					Dispose();
					return Yield.Terminate;
				} else {
					Exception exception = new ApplicationException("Queue returned unexpected: " + ex.EntryType);
					writeQueue.SetException(exception);
					writeQueue.Dispose();
					Dispose();
					throw ex;
				}
			} catch( Exception ex) {
				writeQueue.SetException(ex);
				writeQueue.Dispose();
				Dispose();
				throw;
    		}
		}

        public void Flush()
        {
            if( debug) log.Debug("Before flush write queue " + writeQueue.Count + ", memory " + memory.Position);
            lock (asyncWriteLocker)
            {
                var result = false;
                if (writeFileResult == null)
                {
                    result = true;
                }
                else if (writeFileResult.IsCompleted)
                {
                    writeFileAction.EndInvoke(writeFileResult);
                    writeFileResult = null;
                    result = true;
                }
                if( result)
                {
                    writeFileResult = writeFileAction.BeginInvoke(null, null);
                }
            }
            while (!CompleteAsyncWrite())
            {
                Thread.Sleep(100);
            }
            if (debug) log.Debug("After flush write queue " + writeQueue.Count + ", memory " + memory.Position);
        }

        private void CloseFile( FileStream fs)
        {
            if( fs != null) {
                if(debug) log.Debug("CloseFile() at with " + tickCount + " ticks and length " + fs.Length );
                
                fs.Flush();
                if (!FlushFileBuffers(fs.SafeFileHandle))   // Flush OS file cache to disk.
                {
                    Int32 err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, "Win32 FlushFileBuffers returned error for " + fs.Name);
                }
                fs.Close();
            }
        }
		
	    private long tickCount = 0;
		private int origSleepSeconds = 3;
		private int currentSleepSeconds = 3;
	    private long writeCounter = 0;
		private void WriteToFile() {
			int errorCount = 0;
            if( memory.Position == 0) return;
			do {
			    try { 
					if( debug) log.Debug("Writing buffer size: " + memory.Position);
			        using (memoryLocker.Using())
			        {
                        fs.Write(memory.GetBuffer(), 0, (int)memory.Position);
                        fs.Flush();
                        memory.Position = 0;
			            Interlocked.Increment(ref writeCounter);
			        }
		    		if( errorCount > 0) {
				    	log.Notice(symbol + ": Retry successful."); 
		    		}
		    		errorCount = 0;
		    		currentSleepSeconds = origSleepSeconds;
			    } catch(IOException e) { 
	    			errorCount++;
			    	log.Debug(symbol + ": " + e.Message + "\nPausing " + currentSleepSeconds + " seconds before retry."); 
			    	Factory.Parallel.Sleep(3000);
			    } 
				tickCount++;
			} while( errorCount > 0);
		}
		
		public void Add(TickIO tick) {
			while( !TryAdd(tick)) {
				Thread.Sleep(1);
			}
		}
		
		public bool TryAdd(TickIO tickIO) {
			if( !isInitialized) {
				throw new ApplicationException("Please initialized TickWriter first.");
			}
			TickBinary tick = tickIO.Extract();
			var result = writeQueue.TryEnqueue(ref tick);
            if( result)
            {
                Interlocked.Increment(ref appendCounter);
            }
		    return result;
		}
		
		[Obsolete("Please discontinue use of CanReceive() and simple check the return value of TryAdd() instaed to find out if the add was succesful.",true)]
		public bool CanReceive {
			get {
				return true;
			}
		}
		
		public bool LogTicks = false;
		
		void progressCallback( string text, Int64 current, Int64 final) {
			if( backgroundWorker != null && backgroundWorker.WorkerReportsProgress) {
				progress.UpdateProgress(text,current,final);
				backgroundWorker.ReportProgress(0, progress);
			}
		}
		
		public void Close() {
			Dispose();
		}
		
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private volatile bool isDisposed = false;
		protected virtual void Dispose(bool disposing)
		{
			if (!isDisposed) {
				isDisposed = true;
			    var count = Interlocked.Read(ref writeCounter);
			    var append = Interlocked.Read(ref appendCounter);
                if( debug) log.Debug("Only " + count + " writes but " + append + " appends.");
				if( !isInitialized)
				{
				    return;
				}
				if( debug) log.Debug("Dispose()");
				if( appendTask != null) {
					if( writeQueue != null) {
                        count = Interlocked.Read(ref writeCounter);
                        append = Interlocked.Read(ref appendCounter);
                        if (debug) log.Debug("Only " + count + " writes before enqueue loop but " + append + " appends.");
                        while( writeQueue.Count > 0)
                        {
                            Thread.Sleep(10);
                        }
                        try
                        {
                            while (!writeQueue.TryEnqueue(EventType.Terminate, symbol))
                            {
                                Thread.Sleep(1);
                            }
                        }
                        catch( QueueException ex)
                        {
                            if( ex.EntryType != EventType.Terminate)
                            {
                                throw;
                            }
                        }
					} else {
						appendTask.Stop();
					}
                    count = Interlocked.Read(ref writeCounter);
                    append = Interlocked.Read(ref appendCounter);
                    if (debug) log.Debug("Only " + count + " writes before join but " + append + " appends.");
                    appendTask.Join();
				}
                count = Interlocked.Read(ref writeCounter);
                append = Interlocked.Read(ref appendCounter);
                if (debug) log.Debug("Only " + count + " writes before CompleteAsyncWrite but " + append + " appends.");
                while (!CompleteAsyncWrite())
			    {
			        Thread.Sleep(100);
			    }
                count = Interlocked.Read(ref writeCounter);
                append = Interlocked.Read(ref appendCounter);
                if (debug) log.Debug("Only " + count + " writes before writeToFile but " + append + " appends.");
                if (memory != null && memory.Position > 0)
                {
					WriteToFile();
		    	}
                count = Interlocked.Read(ref writeCounter);
                append = Interlocked.Read(ref appendCounter);
                if (debug) log.Debug("write queue has " + writeQueue.Count + " left over and memory has " + memory.Position + " bytes.");
                if (debug) log.Debug("Only " + count + " writes before closeFile but " + append + " appends.");
                if (fs != null)
                {
                    CloseFile(fs);
                    fs = null;
                    log.Info("Flushed and closed file " + fileName);
                    log.Debug("keepFileOpen - Close()");
		    	}
				if( debug) log.Debug("Exiting Close()");
			}
		}

 		public BackgroundWorker BackgroundWorker {
			get { return backgroundWorker; }
			set { backgroundWorker = value; }
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
   		
		public int MaxCount {
			get { return maxCount; }
			set { maxCount = value; }
		}
		
		public bool KeepFileOpen {
			get { return keepFileOpen; }
			set { /* keepFileOpen = value; */ }
		}
		
		public TickQueue WriteQueue {
			get { return writeQueue; }
		}
	}
}
