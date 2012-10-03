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
using System.Configuration;
using System.IO;
using System.Threading;

using NUnit.Framework;
using TickZoom.Api;
using TickZoom.Charting;
using TickZoom.Common;
using TickZoom.Examples;
using TickZoom.Interceptors;
using TickZoom.Starters;
using TickZoom.Statistics;

#if REALTIME

#endif


namespace TickZoom.StarterTest
{
	[TestFixture]
	public class StarterTest
	{
	    string storageFolder;
		Log log = Factory.SysLog.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
	    public delegate void ShowChartDelegate(ChartControl chart);
		List<ChartThread> chartThreads = new List<ChartThread>();
		
	    public StarterTest() {
    		storageFolder = Factory.Settings["AppDataFolder"];
   			if( storageFolder == null) {
       			throw new ApplicationException( "Must set AppDataFolder property in app.config");
   			}
	    }
		[SetUp]
		public void TestSetup() {
		}
		
		[TearDown]
		public void TearDown() {
		}
		
		[Test]
		public void TestHistorical()
		{
			Starter starter = new HistoricalStarter();
			starter.ProjectProperties.Starter.StartTime = (TimeStamp) new DateTime(2005,1,1);
    		starter.ProjectProperties.Starter.EndTime = (TimeStamp) new DateTime(2006,2,1);
    		starter.DataFolder = "Test";
    		starter.ProjectProperties.Starter.SetSymbols("USD_JPY");
			Interval intervalDefault = Intervals.Hour1;
			starter.ProjectProperties.Starter.IntervalDefault = intervalDefault;
			
			// No charting setup for these tests. Running without charts.
			
			ModelLoaderInterface loader = new OptimizeLoader();
    		starter.Run(loader);
    		Portfolio strategy = loader.TopModel as Portfolio;
    		Assert.AreEqual(33,strategy.Performance.ComboTrades.Count);
    		
		}
		
		[Test]
		public void TestDesign()
		{
			long start = Factory.TickCount;
			Starter starter = new DesignStarter();
			ModelInterface model = new TestSimpleStrategy();
		    starter.DataFolder = "Test";
    		starter.Run(model);
    		long elapsed = Factory.TickCount - start;
    		Assert.Less( elapsed, 10000);
    		// Verify the chart data.
    		Assert.IsNotNull( model.Data);
    		Assert.IsNotNull( model.Bars);
		}
		
		[Test]
		public void TestOptimize()
		{
            Assert.Ignore();
			Starter starter = new OptimizeStarter();
			var profitLossLogic = new ProfitLossCallback2();
    		starter.ProjectProperties.Starter.StartTime = (TimeStamp) new DateTime(2005,1,1);
    		starter.ProjectProperties.Starter.EndTime = (TimeStamp) new DateTime(2006,2,1);
			starter.ProjectProperties.Starter.IntervalDefault = Intervals.Hour1;
    		starter.DataFolder = "Test";
    		starter.ProjectProperties.Starter.SetSymbols("USD_JPY");
    		starter.ProjectProperties.Starter.SymbolProperties[0].ProfitLoss = profitLossLogic;
            FillSimulatorPhysical.MaxPartialFillsPerOrder = 10;
            starter.Run(new OptimizeLoader());
    		Assert.IsTrue(FileCompare(storageFolder+@"\Statistics\optimizeResults.csv",@"..\..\Platform\TickZoomTesting\Startup\optimizeResults.csv"));
		}
		
		[Test]
		public void TestOptimizeBadVariable()
		{
			Thread.Sleep(2000); // Delay for file lock to get released.
			Starter starter = new OptimizeStarter();
    		starter.ProjectProperties.Starter.StartTime = (TimeStamp) new DateTime(2005,1,1);
    		starter.ProjectProperties.Starter.EndTime = (TimeStamp) new DateTime(2006,2,1);
			starter.ProjectProperties.Starter.IntervalDefault = Intervals.Hour1;
    		starter.DataFolder = "Test";
    		starter.ProjectProperties.Starter.SetSymbols("USD_JPY");
    		try { 
	    		starter.Run(new OptimizeLoaderBad());
	    		Assert.Fail("Supposed to throw an exception about a bad optimize variable.");
    		} catch( ApplicationException ex) {
    			Assert.AreEqual("Error, setting optimize variables.",ex.Message);
    		}
    		Assert.IsFalse(File.Exists(storageFolder+@"\Statistics\optimizeResults.csv"));
		}
		
		[Test]
		public void TestGeneticBadVariable()
		{
			Thread.Sleep(2000); // Delay for file lock to get released.
			Starter starter = new GeneticStarter();
    		starter.ProjectProperties.Starter.StartTime = (TimeStamp) new DateTime(2005,1,1);
    		starter.ProjectProperties.Starter.EndTime = (TimeStamp) new DateTime(2006,2,1);
			starter.ProjectProperties.Starter.IntervalDefault = Intervals.Hour1;
    		starter.DataFolder = "Test";
    		starter.ProjectProperties.Starter.SetSymbols("USD_JPY");
    		try { 
	    		starter.Run(new OptimizeLoaderBad());
	    		Assert.Fail("Supposed to throw an exception about a bad optimize variable.");
    		} catch( ApplicationException ex) {
    			Assert.AreEqual("Error, setting optimize variables.",ex.Message);
    		}
    		Assert.IsFalse(File.Exists(storageFolder+@"\Statistics\optimizeResults.csv"));
		}
		
		[Test]
		public void TestRealTime()
		{
#if REALTIME
			ProviderProxy provider = new ProviderProxy();

			Starter starter = new RealTimeStarter();
    		starter.ProjectProperties.Starter.StartTime = (TimeStamp) new DateTime(2004,1,1);
    		starter.ProjectProperties.Starter.EndTime = (TimeStamp) new DateTime(2004,2,1);
			starter.ProjectProperties.Starter.IntervalDefault = Intervals.Hour1;
			starter.AddDataFeed(provider);
			// No charting for these tests.
    		starter.DataFolder = "Test";
    		starter.ProjectProperties.Starter.SetSymbols(USD_JPY";
	   		starter.Run(new OptimizeLoader());	
#endif	   		
		}
		
		[Test]
		public void TestGenetic()
		{
            Assert.Ignore();
			GeneticStarter geneticStarter = new GeneticStarter();
			geneticStarter.SetRandomSeed(9999);
			geneticStarter.TotalPasses = 100;
			
			Starter starter = geneticStarter;
    		starter.ProjectProperties.Starter.StartTime = (TimeStamp) new DateTime(2005,1,1);
    		starter.ProjectProperties.Starter.EndTime = (TimeStamp) new DateTime(2006,2,1);
			starter.ProjectProperties.Starter.IntervalDefault = Intervals.Hour1;
     		starter.DataFolder = "Test";
     		starter.ProjectProperties.Starter.SetSymbols("USD_JPY");
    		starter.Run(new GeneticLoader());
    		Assert.IsTrue(FileCompare(storageFolder+@"\Statistics\optimizeResults.csv",@"..\..\Platform\TickZoomTesting\Startup\geneticResults.csv"));
		}
	    
		[Test]
		public void TestOneGeneticPass()
		{
			var starter = new HistoricalStarter();
    		starter.ProjectProperties.Starter.StartTime = (TimeStamp) new DateTime(2005,1,1);
    		starter.ProjectProperties.Starter.EndTime = (TimeStamp) new DateTime(2006,2,1);
			starter.ProjectProperties.Starter.IntervalDefault = Intervals.Hour1;
     		starter.DataFolder = "Test";
     		starter.ProjectProperties.Starter.SetSymbols("USD_JPY");
    		starter.Run(new GeneticLoader());
//    		Assert.IsTrue(FileCompare(storageFolder+@"\Statistics\optimizeResults.csv",@"..\..\Platform\TickZoomTesting\Startup\geneticResults.csv"));
		}
		
		public class OptimizeLoader : ModelLoaderCommon {
			public OptimizeLoader() {
				category = "Test";
				name = "Optimize";
				IsVisibleInGUI = false;
			}
			
			public override void OnInitialize(ProjectProperties properties) {
				AddVariable("ExampleReversalStrategy.ExitStrategy.StopLoss",0.01,1.00,0.10,0.25,true);
				AddVariable("ExampleReversalStrategy.ExitStrategy.TargetProfit",0.01,1.00,0.10,0,false);
			}		
			
			public override void OnLoad(ProjectProperties projectProperties)
			{
				var strategy = new ExampleReversalStrategy();
				var portfolio = new Portfolio();
		    	portfolio.AddDependency(strategy);
				portfolio.Performance.Equity.EnableYearlyStats = true;
				portfolio.Performance.Equity.EnableMonthlyStats = true;
				portfolio.Performance.Equity.EnableWeeklyStats = true;
				portfolio.Performance.Equity.EnableDailyStats = true;
		    	TopModel = portfolio;
			}
		}
		
		public class GeneticLoader : ModelLoaderCommon {
			public GeneticLoader() {
				category = "Test";
				name = "Genetic Optimize";
				IsVisibleInGUI = true;
			}
			
			public override void OnInitialize(ProjectProperties properties) {
				AddVariable("ExampleReversalStrategy.ExitStrategy.StopLoss",0.01,1.00,0.10,0.25,true);
				AddVariable("ExampleReversalStrategy.ExitStrategy.TargetProfit",0.01,100.00,0.01,0,true);
			}		
			
			public override void OnLoad(ProjectProperties projectProperties)
			{
				var strategy = new ExampleReversalStrategy();
				var portfolio = new Portfolio();
				portfolio.AddDependency( strategy);
				portfolio.Performance.Equity.EnableYearlyStats = true;
				portfolio.Performance.Equity.EnableMonthlyStats = true;
				portfolio.Performance.Equity.EnableWeeklyStats = true;
				portfolio.Performance.Equity.EnableDailyStats = true;
		    	TopModel = portfolio;
			}
		}
		
		public class OptimizeLoaderBad : ModelLoaderCommon {
			public OptimizeLoaderBad() {
				IsVisibleInGUI = false;
			}	
			
			public override void OnInitialize(ProjectProperties properties) {
				AddVariable("ExampleReversalStrategy.xxxx.StopLoss",0.01,1.00,0.10,0.25,true);
				AddVariable("ExampleReversalStrategy.xxxx.TargetProfit",0.01,1.00,0.10,0.25,true);
			}		
			
			public override void OnLoad(ProjectProperties projectProperties)
			{
				var strategy = new ExampleReversalStrategy();
				var portfolio = new Portfolio();
				portfolio.AddDependency( strategy);
		    	TopModel = portfolio;
			}
			
		}
		
		// This method accepts two strings the represent two files to 
		// compare. A return value of 0 indicates that the contents of the files
		// are the same. A return value of any other value indicates that the 
		// files are not the same.
		private bool FileCompare(string file1, string file2)
		{
		     string file1byte;
		     string file2byte;
		
		     // Determine if the same file was referenced two times.
		     if (file1 == file2)
		     {
		          // Return true to indicate that the files are the same.
		          return true;
		     }
		               
		     // Open the two files.
		     var fs1 = new StreamReader(file1);
		     var fs2 = new StreamReader(file2);
		          
		     // Read and compare a byte from each file until either a
		     // non-matching set of bytes is found or until the end of
		     // file1 is reached.
		     do 
		     {
		          // Read one byte from each file.
		         file1byte = fs1.ReadLine();
		         file2byte = fs2.ReadLine();
		     }
		     while ((file1byte == file2byte) && file1byte != null && file2byte != null);
		     
		     // Close the files.
		     fs1.Close();
		     fs2.Close();
		
		     // Return the success of the comparison. "file1byte" is 
		     // equal to "file2byte" at this point only if the files are 
		        // the same.
		    return file1byte == file2byte;
		}
	}
}
