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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Loaders;
using NUnit.Framework;
using TickZoom.Api;
using TickZoom.GUI;
using TickZoom.Presentation;
using TickZoom.TickUtil;
using ZedGraph;

namespace Other
{
	public static class StarterConfigTestExtensions {
		public static void WaitComplete(this StarterConfig config, int seconds) {
			config.WaitComplete(seconds,null);
		}
		
		public static void WaitComplete(this StarterConfig config, int seconds, Func<bool> onCompleteCallback) {
			long end = Factory.TickCount + (seconds * 1000);
			while( Factory.TickCount < end) {
				config.Catch();
				if( onCompleteCallback != null && onCompleteCallback()) {
					return;
				}
				Thread.Sleep(1);
			}
		}
		
		public static void Pause(this StarterConfig config, int seconds) {
			long end = Factory.TickCount + (seconds * 1000);
			long current;
			while( (current = Factory.TickCount) < end) {
				Application.DoEvents();
				config.Catch();
				Thread.Sleep(1);
			}
		}		
	}
		
	[TestFixture]
	public class GUITest
	{
		private static Log log = Factory.SysLog.GetLogger(typeof(GUITest));
		private bool debug = log.IsDebugEnabled;
		private Thread guiThread;
		private Execute execute;
		[SetUp]
		public void Setup() {
			DeleteFiles();
			Process[] processes = Process.GetProcessesByName("TickZoomCombinedMock");
    		foreach( Process proc in processes) {
    			proc.Kill();
    		}
		}
		
		private StarterConfig CreateSimulateConfig() {
			var config = new StarterConfig("test");
            //config.ServiceConfig = "WarehouseTest.config";
            //config.ServicePort = servicePort;
            //config.ProviderAssembly = "TickZoomCombinedMock";
			
			WaitForEngine(config);
			return config;
		}

		public void WaitForEngine(StarterConfig config) {
			while( !config.IsEngineLoaded) {
				Thread.Sleep(1);
				Application.DoEvents();
			}
		}
		
		[Test]
		public void TestConfigRealTimeNoHistorical()
		{
            try
            {
                var config = CreateSimulateConfig();
                config.SymbolList = "IBM,GBP/USD";
                StarterConfigView form;
                StartGUI(config, out form);
                StrategyBaseTest.CleanupFiles(config.SymbolList, null);
                config.DefaultPeriod = 10;
                config.DefaultBarUnit = BarUnit.Tick.ToString();
                config.ModelLoader = "Example: Reversal Multi-Symbol";
                config.StarterName = "FIXSimulatorStarter";
                config.Start();
                config.WaitComplete(120, () => { return config.CommandWorker.IsBusy; });
                config.Stop();
                config.WaitComplete(120, () => { return !config.CommandWorker.IsBusy; });
                Assert.IsFalse(config.CommandWorker.IsBusy, "ProcessWorker.Busy");
			} catch( Exception ex) {
				log.Error("Test failed with error: " + ex.Message, ex);
				Environment.Exit(1);
            } finally {
                execute.Exit();
                guiThread.Join();
                Factory.Release();
            }
		}
		
		[Test]
		public void TestGUIRealTimeNoHistorical()
		{
			var config = CreateSimulateConfig();
			try {
	            StarterConfigView form;
	            StartGUI(config, out form);
				config.SymbolList = "IBM,GBP/USD";
                StrategyBaseTest.CleanupFiles(config.SymbolList, null);
                config.DefaultPeriod = 10;
				config.DefaultBarUnit = BarUnit.Tick.ToString();
				config.ModelLoader = "Example: Reversal Multi-Symbol";
                config.StarterName = "FIXSimulatorStarter";
				config.Start();
				config.WaitComplete(120, () => { return config.CommandWorker.IsBusy; } );
                Thread.Sleep(5000);
				config.Stop();
				config.WaitComplete(120, () => { return !config.CommandWorker.IsBusy; } );
				Assert.IsFalse(config.CommandWorker.IsBusy,"ProcessWorker.Busy");
            }
            catch (Exception ex)
            {
                log.Error("Test failed with error: " + ex.Message, ex);
                Environment.Exit(1);
            }
            finally
            {
				execute.Exit();
				guiThread.Join();
                Factory.Release();
            }
		}
		
		[Test]
		public void TestGUIRealTimeDemo()
		{
			Assert.Ignore();
            StrategyBaseTest.CleanupFiles(null, null);
            try
            {
                while (true)
                {
                    TestGUIIteration();
                }
            }
            catch (Exception ex)
            {
                log.Error("Test failed with error: " + ex.Message, ex);
                Environment.Exit(1);
            }
            finally
            {
                Factory.Release();
            }
		}

		private void StartGUI( StarterConfig config, out StarterConfigView outForm) {
			WaitForEngine(config);
			var isRunning = false;
			StarterConfigView form = null;
			guiThread = new Thread( () => {
			    Thread.CurrentThread.Name = "GUIThread";
			    execute = Execute.Create();
	            form = new StarterConfigView(execute,config);
	            AutoModelBinder.Bind( config, form, execute);
	            Application.Idle += execute.MessageLoop;
	            form.Visible = false;
	            isRunning = true;
	            Application.Run();
			});
			guiThread.Start();
			config.WaitComplete(30, () => { return isRunning; } );
			outForm = form;
		}
		
		public void TestGUIIteration() {
			var appData = Factory.Settings["PriceDataFolder"];
 			File.Delete( appData + @"\ServerCache\IBM.tck");
			var config = new StarterConfig();
			StarterConfigView form = null;
			StartGUI(config, out form);
			try {
				config.WaitComplete(2);
				config.SymbolList = "IBM";
                StrategyBaseTest.CleanupFiles(config.SymbolList, null);
                config.DefaultPeriod = 10;
				config.DefaultBarUnit = BarUnit.Second.ToString();
				config.ModelLoader = "Example: Breakout Reversal";
				config.StarterName = "Realtime Operation (Demo or Live)";
				config.Start();
				config.WaitComplete(30, () => { return form.PortfolioDocs.Count > 0; } );
				Assert.Greater(form.PortfolioDocs.Count,0);
				var chart = form.PortfolioDocs[0].ChartControl;
				config.WaitComplete(30, () => { return chart.IsDrawn; } );
	     		var pane = chart.DataGraph.MasterPane.PaneList[0];
	    		Assert.IsNotNull(pane.CurveList);
				config.WaitComplete(30, () => { return pane.CurveList.Count > 0; } );
				Assert.Greater(pane.CurveList.Count,0);
	    		var chartBars = (OHLCBarItem) pane.CurveList[0];
				config.WaitComplete(60, () => { return chartBars.NPts >= 3; } );
	    		Assert.GreaterOrEqual(chartBars.NPts,3);
				config.Stop();
				config.WaitComplete(30, () => { return !config.CommandWorker.IsBusy; } );
				Assert.IsFalse(config.CommandWorker.IsBusy,"ProcessWorker.Busy");
			} catch( Exception ex) {
				log.Error("Test failed with error: " + ex.Message, ex);
				Environment.Exit(1);
			} finally {
				execute.Exit();
				guiThread.Join();
			}
		}
		
		public void TestRealTimeNoHistorical()
		{
			var config = CreateSimulateConfig();
			config.SymbolList = "IBM,GBP/USD";
            StrategyBaseTest.CleanupFiles(config.SymbolList, null);
            config.DefaultPeriod = 10;
			config.DefaultBarUnit = BarUnit.Tick.ToString();
			config.ModelLoader = "Example: Reversal Multi-Symbol";
            config.StarterName = "FIXSimulatorStarter";
			config.Start();
			config.WaitComplete(10);
			config.Stop();
			config.WaitComplete(120, () => { return !config.CommandWorker.IsBusy; } );
			Assert.IsFalse(config.CommandWorker.IsBusy,"ProcessWorker.Busy");
		}
		
		private void DeleteFiles() {
			string appData = Factory.Settings["AppDataFolder"];
 			StrategyBaseTest.DeleteFile( appData + @"\Test\\ServerCache\ESZ9.tck");
            StrategyBaseTest.DeleteFile(appData + @"\Test\\ServerCache\IBM.tck");
            StrategyBaseTest.DeleteFile(appData + @"\Test\\ServerCache\GBPUSD.tck");
 			Directory.CreateDirectory(appData + @"\Workspace\");
            StrategyBaseTest.DeleteFile(appData + @"\Workspace\test.config");
		}
		
		[Test]
		public void TestCapturedDataMatchesProvider()
		{
            Assert.Ignore();
            try
			{
                var config = CreateSimulateConfig();
	            StarterConfigView form;
	            StartGUI(config, out form);
				config.SymbolList = "/ESZ9";
                StrategyBaseTest.CleanupFiles(config.SymbolList, null);
                config.DefaultPeriod = 1;
				config.DefaultBarUnit = BarUnit.Minute.ToString();
				config.EndDateTime = DateTime.UtcNow;
				config.ModelLoader = "Example: Reversal Multi-Symbol";
                config.StarterName = "FIXSimulatorStarter";
				config.Start();
				config.WaitComplete(short.MaxValue);
				config.Stop();
				config.WaitComplete(1200, () => { return !config.CommandWorker.IsBusy; } );
				Assert.IsFalse(config.CommandWorker.IsBusy,"ProcessWorker.Busy");
				string appData = Factory.Settings["AppDataFolder"];
				string compareFile1 = appData + @"\Test\MockProviderData\ESZ9.tck";
				string compareFile2 = appData + @"\Test\ServerCache\ESZ9.tck";
				using ( var reader1 = Factory.TickUtil.TickFile()) {
					reader1.Initialize(compareFile1,config.SymbolList,TickFileMode.Read);
				    var tickIO = Factory.TickUtil.TickIO();
					try {
						int count = 0;
						while(reader1.TryReadTick(tickIO))
						{
							count++;
						}
					} catch( QueueException ex) {
                        Assert.IsTrue(ex.EntryType == EventType.StartHistorical || ex.EntryType == EventType.EndHistorical,"start or end historical");
					}
				}
				using ( var reader1 = Factory.TickUtil.TickFile())
				using ( var reader2 = Factory.TickUtil.TickFile()) {
					reader1.Initialize(compareFile1,TickFileMode.Read);
					reader2.Initialize(compareFile2,TickFileMode.Read);
				    var tickIO1 = Factory.TickUtil.TickIO();
				    var tickIO2 = Factory.TickUtil.TickIO();
					bool result = true;
					int count = 0;
					while(reader1.TryReadTick(tickIO1) && reader2.TryReadTick(tickIO2)) {
						TimeStamp ts1 = new TimeStamp(tickIO1.UtcTime);
						TimeStamp ts2 = new TimeStamp(tickIO2.UtcTime);
						if( !ts1.Equals(ts2)) {
							result = false;
							log.Error("Tick# " + count + " failed. Expected: " + ts1 + ", But was:" + ts2);
						}
						count++;
					}
					Assert.IsTrue(result,"Tick mismatch errors. See log file.");
				}
			} catch( Exception ex) {
				log.Error("Test failed with error: " + ex.Message, ex);
				Environment.Exit(1);
			}
            finally
			{
                Factory.Release();
            }
		}
	}
}
