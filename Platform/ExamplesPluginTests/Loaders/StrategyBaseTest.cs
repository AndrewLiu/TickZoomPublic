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
using System.Text;
using System.Threading;
using System.Windows.Forms;
using NUnit.Framework;
using TickZoom.Api;
using TickZoom.Charting;
using TickZoom.Common;
using TickZoom.GUI;
using TickZoom.Interceptors;
using TickZoom.Presentation;
using TickZoom.Starters;
using TickZoom.Statistics;
using TickZoom.TickUtil;
using TickZoom.Transactions;
using ZedGraph;
using Symbol = TickZoom.Api.Symbol;

namespace Loaders
{
    public class StrategyBaseTest
    {
        static readonly Log log = Factory.SysLog.GetLogger(typeof(StrategyBaseTest));
        readonly bool debug = log.IsDebugEnabled;
        private ModelLoaderInterface loader;
        private string testFileName;
        string dataFolder = "Test";
        string symbols;
        List<PortfolioDoc> portfolioDocs = new List<PortfolioDoc>();
        Dictionary<string,List<StatsInfo>> goodStatsMap = new Dictionary<string,List<StatsInfo>>();
        Dictionary<string,List<StatsInfo>> testStatsMap = new Dictionary<string,List<StatsInfo>>();
        Dictionary<string,List<BarInfo>> goodBarDataMap = new Dictionary<string,List<BarInfo>>();
        Dictionary<string,List<BarInfo>> testBarDataMap = new Dictionary<string,List<BarInfo>>();
        Dictionary<string,List<TradeInfo>> goodTradeMap = new Dictionary<string,List<TradeInfo>>();
        Dictionary<string,List<TradeInfo>> testTradeMap = new Dictionary<string,List<TradeInfo>>();
        Dictionary<string,FinalStatsInfo> goodFinalStatsMap = new Dictionary<string,FinalStatsInfo>();
        Dictionary<string,FinalStatsInfo> testFinalStatsMap = new Dictionary<string,FinalStatsInfo>();
        Dictionary<string,List<TransactionInfo>> goodTransactionMap = new Dictionary<string,List<TransactionInfo>>();
        Dictionary<string,List<TransactionInfo>> testTransactionMap = new Dictionary<string,List<TransactionInfo>>();
        public bool ShowCharts = false;
        private bool storeKnownGood = true;
        public CreateStarterCallback createStarterCallback;
        protected bool testFailed = false;		
        private TimeStamp startTime = new TimeStamp(1800,1,1);
        private TimeStamp endTime = TimeStamp.UtcNow;
        private Elapsed relativeEndTime = default(Elapsed);
        private Interval intervalDefault = Intervals.Minute1;
        private ModelInterface topModel = null;
        private AutoTestMode autoTestMode = AutoTestMode.Historical;
        //private long realTimeOffset;
        private StarterConfig config;
        private bool ignoreMissingKnownGood;
        private Thread guiThread;
        private Execute execute;
        private int testFinshedTimeout;
        private AutoTestSettings testSettings;
        private string testName;


        public StrategyBaseTest( string testName, AutoTestSettings testSettings )
        {
            Factory.IsAutomatedTest = true;
            this.testName = testName;
            this.testSettings = testSettings;
            this.autoTestMode = testSettings.Mode;
            this.loader = testSettings.Loader;
            this.symbols = testSettings.Symbols;
            this.storeKnownGood = testSettings.StoreKnownGood;
		    this.ignoreMissingKnownGood = testSettings.IgnoreMissingKnownGood;
            this.ShowCharts = testSettings.ShowCharts;
            this.startTime = testSettings.StartTime;
            this.endTime = testSettings.EndTime;
            this.relativeEndTime = testSettings.RelativeEndTime;
            this.testFinshedTimeout = testSettings.TestFinishedTimeout;
            this.testFileName = string.IsNullOrEmpty(testSettings.KnownGoodName) ? testSettings.Name : testSettings.KnownGoodName;
            this.intervalDefault = testSettings.IntervalDefault;
            createStarterCallback = CreateStarter;
            StaticGlobalFlags.isWriteFinalStats = false;
            FillSimulatorPhysical.MaxPartialFillsPerOrder = 10;
        }
		
        public StrategyBaseTest() {
            testFileName = GetType().Name;
            createStarterCallback = CreateStarter;
            StartGUIThread();
        }
		
        private Starter CreateStarter() {
            return new HistoricalStarter();			
        }

        public void StartGUIThread() {
            var isRunning = false;
            guiThread = new Thread( () => {
                                              execute = Execute.Create();
                                              Application.Idle += execute.MessageLoop;
                                              isRunning = true;
                                              Application.Run();
            });
            guiThread.Name = "GUIThread";
            guiThread.Start();
            while( !isRunning) {
                Thread.Sleep(1);
            }
        }
		
        public void StopGUIThread() {
            execute.Exit();
            guiThread.Join();
            guiThread.Abort();
        }
		
        public StarterConfig SetupConfigStarter(AutoTestMode autoTestMode) {
            // Set run properties 
            var config = new StarterConfig("test");
            config.ServicePort = 6490;
            config.EndDateTime = endTime.DateTime;
            config.StartDateTime = startTime.DateTime;
            config.TestFinishedTimeout = testFinshedTimeout;
            config.SimulatorProperties = testSettings.SimulatorProperties;
            while (config.IsBusy)
            {
                Thread.Sleep(100);
            }
            config.AutoUpdate = true;
            while(config.IsBusy)
            {
                Thread.Sleep(100);
            }
            switch( autoTestMode) {
                case AutoTestMode.Historical:
                    config.StarterName = "HistoricalStarter";
                    break;
                case AutoTestMode.NegativeMBT:
                    config.StarterName = "MBTSimulatorStarter";
                    config.SimulatorProperties.EnableNegativeTests = true;
                    break;
                case AutoTestMode.NegativeLime:
                    config.StarterName = "LimeSimulatorStarter";
                    config.SimulatorProperties.EnableNegativeTests = true;
                    break;
                case AutoTestMode.SimulateMBT:
                    config.StarterName = "MBTSimulatorStarter";
                    break;
                case AutoTestMode.SimulateLime:
                    config.StarterName = "LimeSimulatorStarter";
                    break;
                case AutoTestMode.FIXPlayBack:
                    config.StarterName = "FIXPlayBackStarter";
                    if( relativeEndTime != default(Elapsed)) {
                        var relative = TimeStamp.UtcNow;
                        relative.Add( relativeEndTime);
                        config.EndDateTime = relative.DateTime;
                    }
                    break;			
                default:
                    throw new ApplicationException("AutoTestMode " + autoTestMode + " is unknown.");
            }
			
            config.CreateChart = HistoricalCreateChart;
            config.ShowChart = HistoricalShowChart;
    		
            config.DataSubFolder = "Test";
            config.SymbolList = Symbols;
            config.DefaultPeriod = intervalDefault.Period;
            config.DefaultBarUnit = intervalDefault.BarUnit.ToString();
            config.ModelLoader = loader.Name;
            //config.Initialize();
            return config;
        }

        public Starter SetupDesignStarter()
        {
            Starter starter = new DesignStarter();
            // Set run properties as in the GUI.
            starter.ProjectProperties.Starter.StartTime = startTime;
            starter.ProjectProperties.Starter.EndTime = endTime;

            starter.DataFolder = "Test";
            starter.ProjectProperties.Starter.TryAddSymbols(Symbols);
            starter.ProjectProperties.Starter.IntervalDefault = intervalDefault;
            starter.CreateChartCallback = new CreateChartCallback(HistoricalCreateChart);
            starter.ShowChartCallback = new ShowChartCallback(HistoricalShowChart);
            return starter;
        }
			
        [TestFixtureSetUp]
        public virtual void RunStrategy() {
            log.Notice("Beginning RunStrategy() for " + testName);
            SyncTicks.Success = true;
            SyncTicks.CurrentTestName = testName;
            try
            {
                StaticGlobal.Clear();
                CleanupFiles(Symbols, null);
                StartGUIThread();
                // Clear tick syncs.
                foreach (var tickSync in SyncTicks.TickSyncs)
                {
                    tickSync.Value.ForceClear("RunStrategy");
                }
                try
                {
                    // Run the loader.
                    try
                    {
                        config = SetupConfigStarter(autoTestMode);
                        while (config.IsBusy)
                        {
                            Thread.Sleep(10);
                        }
                        config.Start();
                        var tempStarter = config.Starter;
                        while (config.IsBusy)
                        {
                            Thread.Sleep(10);
                        }
                        topModel = config.TopModel;

                    }
                    catch (ApplicationException ex)
                    {
                        if (ex.Message.Contains("not found"))
                        {
                            Assert.Ignore("LoaderName could not be loaded.");
                            return;
                        }
                        else
                        {
                            log.Error("StrategyBaseTest failed: ", ex);
                            throw;
                        }
                    }
                    finally
                    {
                        if (config != null)
                        {
                            config.Stop();
                        }
                        Factory.UserLog.Flush();
                        Factory.SysLog.Flush();
                    }

                    WriteHashes();

                    WriteFinalStats();

                    LoadTransactions();
                    LoadTrades();
                    LoadBarData();
                    LoadStats();
                    LoadFinalStats();
                }
                catch (AssertionException ex)
                {
                    log.Error(ex.Message);
                    testFailed = true;
                    throw;
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message, ex);
                    testFailed = true;
                    throw;
                }
            }
            catch( Exception ex)
            {
                log.Error("Exception while running test: " + ex.Message, ex);
                if( !System.Diagnostics.Debugger.IsAttached)
                {
                Environment.Exit(1);
            }
        }
        }

        private ModelLoaderInterface GetLoaderInstance() {
            var type = loader.GetType();
            return (ModelLoaderInterface) type.Assembly.CreateInstance(type.FullName);
        }

        public static void CleanupFiles(string symbols, string dummyArg) {
            CleanupServerCache(symbols);
            DeleteFiles("MBTFIXProvider");
            DeleteFiles("LimeFIXProvider");
        }

        private static void DeleteFiles(string fileName) {
            var appDataFolder = Factory.Settings["AppDataFolder"];
            if( Directory.Exists(appDataFolder + Path.DirectorySeparatorChar + "MockProviderData"))
            {
                Directory.Delete(appDataFolder + Path.DirectorySeparatorChar + "MockProviderData", true);
            }
            var providersFolder = Path.Combine(appDataFolder,"Providers");
            var providerServiceFolder = Path.Combine(providersFolder, "ProviderService");
            var warehouseTestConfig = Path.Combine(providerServiceFolder, "WarehouseTest.config");

            var mbtfixFolder = Path.Combine(providersFolder, fileName);
            var databaseFolder = Path.Combine(appDataFolder, "Database");
            Directory.CreateDirectory(databaseFolder);
            var filePaths = Directory.GetFiles(databaseFolder, "*.dat*", SearchOption.TopDirectoryOnly);
            foreach( var path in filePaths)
            {
                DeleteFile(path);
            }
            var filePath = Path.Combine(mbtfixFolder, "LoginFailed.txt");
            DeleteFile(filePath);
            DeleteFile(warehouseTestConfig);
        }
		
        private static void CleanupServerCache(string symbols) {
            if (symbols == null) return;
            string appData = Factory.Settings["AppDataFolder"];
            var symbolStrings = symbols.Split(new char[] { ',' });
            foreach( var fullSymbol in symbolStrings) {
                var symbolParts = fullSymbol.Split(new char[] { '.' });
                var symbol = symbolParts[0];
                var symbolFile = symbol.Trim().StripInvalidPathChars();
                DeleteFile( appData + @"\Test\\ServerCache\" + symbolFile + ".tck");
            }
        }

        public void SimulatorSuccess( string strategyName)
        {
            Assert.IsTrue(SyncTicks.Success);
        }

        public static void DeleteFile(string path)
        {
            var errors = new List<Exception>();
            var errorCount = 0;
            while (errorCount < 30)
            {
                try
                {
                    log.Info("Deleting (" + errorCount + ") " + path);
                    File.Delete(path);
                    errors.Clear();
                    break;
                }
                catch (DirectoryNotFoundException)
                {
                    errors.Clear();
                    break;
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    Thread.Sleep(1000);
                    errorCount++;
                }
            }
            if (errors.Count > 0)
            {
                var ex = errors[errors.Count - 1];
                log.Error("Can't delete " + path, ex);
                Factory.Parallel.StackTrace();
                throw new IOException("Can't delete " + path, ex);
            }
        }
		
        [TestFixtureTearDown]
        public virtual void EndStrategy()
        {
            var unknownCount = 0;
            foreach (var kvp in log.UniqueTypes)
            {
                if (kvp.Value.UnknownType)
                {
                    log.Info("Unknown log argument type: " + kvp.Key.FullName + ", " + kvp.Value.Count);
                    unknownCount++;
                }
            }
            Assert.AreEqual(0,unknownCount,"Number of unknown logging types.");
            var uniqueFormats = 0;
            foreach (var kvp in log.UniqueFormats)
            {
                if (kvp.Value.Count < 10)
                {
                    log.Info("Possibly malformed log format: " + kvp.Key + ", " + kvp.Value.Count);
                    uniqueFormats++;
                }
            }
            Assert.Less(uniqueFormats, 250, "Number of unique string formats.");
            if (ShowCharts)
            {
                log.Warn("Popped up MessageBox: Finished with Charts?");
                MessageBox.Show("Finished with Charts?");
            }
            if (config != null)
            {
                config.Stop();
            }
            StopGUIThread();
            HistoricalCloseCharts();
            Factory.Release();
            goodStatsMap.Clear();
            testStatsMap.Clear();
            goodBarDataMap.Clear();
            testBarDataMap.Clear();
            goodTradeMap.Clear();
            testTradeMap.Clear();
            goodFinalStatsMap.Clear();
            testFinalStatsMap.Clear();
            goodTransactionMap.Clear();
            testTransactionMap.Clear();
            topModel = null;
            Factory.UserLog.Flush();
            Factory.SysLog.Flush();
            TickFileBlocked.VerifyClosed();
            if (testFailed)
            {
                log.Error("Exiting because one of the tests failed.");
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                Environment.Exit(1);
            }
        }
        }
		
        public class TransactionInfo {
            public string Symbol;
            public LogicalFillBinary Fill;
        }
		
        public class TradeInfo {
            public double ClosedEquity;
            public double ProfitLoss;
            public TransactionPairBinary Trade;
        }
		
        public class BarInfo {
            public TimeStamp Time;
            public TimeStamp EndTime;
            public double Open;
            public double High;
            public double Low;
            public double Close;
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(Time + ", ");
                sb.Append(EndTime + ", ");
                sb.Append(Open + ", ");
                sb.Append(High + ", ");
                sb.Append(Low + ", ");
                sb.Append(Close);
                return sb.ToString();
            }
        }
		
        public class FinalStatsInfo {
            public double StartingEquity;
            public double ClosedEquity;
            public double OpenEquity;
            public double CurrentEquity;
        }
		
        public class StatsInfo {
            public TimeStamp Time;
            public double ClosedEquity;
            public double OpenEquity;
            public double CurrentEquity;
        }
		
        public void WriteHashes() {
            foreach( var model in GetAllModels(topModel)) {
                Performance performance;
                if( model is Strategy) {
                    performance = ((Strategy) model).Performance;
                } else if( model is Portfolio) {
                    performance = ((Portfolio) model).Performance;
                } else {
                    continue;
                }
                log.Info( model.Name + " bar hash: " + performance.GetBarsHash());
                log.Info( model.Name + " stats hash: " + performance.GetStatsHash());
            }
        }
		
        public void WriteFinalStats() {
            string newPath = Factory.SysLog.LogFolder + @"\FinalStats.log";
            using( var writer = new StreamWriter(newPath)) {
                foreach( var model in GetAllModels(topModel)) {
                    Performance performance;
                    if( model is Strategy) {
                        performance = ((Strategy) model).Performance;
                    } else if( model is Portfolio) {
                        performance = ((Portfolio) model).Performance;
                    } else {
                        continue;
                    }
                    writer.Write(model.Name);
                    writer.Write(",");
                    writer.Write(performance.Equity.StartingEquity);
                    writer.Write(",");
                    writer.Write(performance.Equity.ClosedEquity);
                    writer.Write(",");
                    writer.Write(performance.Equity.OpenEquity);
                    writer.Write(",");
                    writer.Write(performance.Equity.CurrentEquity);
                    writer.WriteLine();
                }
            }
            StaticGlobalFlags.isWriteFinalStats = true;
        }
		
        public void LoadFinalStats() {
            string fileDir = @"..\..\Platform\ExamplesPluginTests\Loaders\Trades\";
            string knownGoodPath = fileDir + testFileName + "FinalStats.log";
            string newPath = Factory.SysLog.LogFolder + @"\FinalStats.log";
            if( File.Exists(newPath)) {
                if( storeKnownGood) {
                    File.Copy(newPath,knownGoodPath,true);
                }
                testFinalStatsMap.Clear();
                LoadFinalStats(newPath,testFinalStatsMap);
            }
            if( File.Exists(knownGoodPath)) {
                goodFinalStatsMap.Clear();
                LoadFinalStats(knownGoodPath,goodFinalStatsMap);
            }
        }
		
        public void LoadFinalStats(string filePath, Dictionary<string,FinalStatsInfo> tempFinalStats) {
            using( FileStream fileStream = new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
                StreamReader file = new StreamReader(fileStream);
                string line;
                while( (line = file.ReadLine()) != null) {
                    string[] fields = line.Split(',');
                    int fieldIndex = 0;
                    string strategyName = fields[fieldIndex++];
                    var testInfo = new FinalStatsInfo();
					
                    testInfo.StartingEquity = double.Parse(fields[fieldIndex++]);
                    testInfo.ClosedEquity = double.Parse(fields[fieldIndex++]);
                    testInfo.OpenEquity = double.Parse(fields[fieldIndex++]);
                    testInfo.CurrentEquity = double.Parse(fields[fieldIndex++]);
					
                    tempFinalStats.Add(strategyName,testInfo);
                }
            }
        }
        public void LoadTrades() {
            string fileDir = @"..\..\Platform\ExamplesPluginTests\Loaders\Trades\";
            string knownGoodPath = fileDir + testFileName + "Trades.log";
            string newPath = Factory.SysLog.LogFolder + @"\Trades.log";
            if( File.Exists(newPath)) {
                if( storeKnownGood) {
                    File.Copy(newPath,knownGoodPath,true);
                }
                testTradeMap.Clear();
                LoadTrades(newPath,testTradeMap);
            }
            if( File.Exists(knownGoodPath)) {
                goodTradeMap.Clear();
                LoadTrades(knownGoodPath,goodTradeMap);
            }
        }
		
        public void LoadTrades(string filePath, Dictionary<string,List<TradeInfo>> tempTrades) {
            using( FileStream fileStream = new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
                StreamReader file = new StreamReader(fileStream);
                string line;
                while( (line = file.ReadLine()) != null) {
                    string[] fields = line.Split(',');
                    int fieldIndex = 0;
                    string strategyName = fields[fieldIndex++];
                    TradeInfo testInfo = new TradeInfo();
					
                    testInfo.ClosedEquity = double.Parse(fields[fieldIndex++]);
                    testInfo.ProfitLoss = double.Parse(fields[fieldIndex++]);
					
                    line = string.Join(",",fields,fieldIndex,fields.Length-fieldIndex);
                    testInfo.Trade = TransactionPairBinary.Parse(line);
                    List<TradeInfo> tradeList;
                    if( tempTrades.TryGetValue(strategyName,out tradeList)) {
                        tradeList.Add(testInfo);
                    } else {
                        tradeList = new List<TradeInfo>();
                        tradeList.Add(testInfo);
                        tempTrades.Add(strategyName,tradeList);
                    }
                }
            }
        }
		
        public void LoadTransactions() {
            string fileDir = @"..\..\Platform\ExamplesPluginTests\Loaders\Trades\";
            string knownGoodPath = fileDir + testFileName + "Transactions.log";
            string newPath = Factory.SysLog.LogFolder + @"\Transactions.log";
            if( File.Exists(newPath)) { 
                if( storeKnownGood) {
                    File.Copy(newPath,knownGoodPath,true);
                }
                testTransactionMap.Clear();
                LoadTransactions(newPath,testTransactionMap);
            }
            if( File.Exists(knownGoodPath)) {
                goodTransactionMap.Clear();
                LoadTransactions(knownGoodPath,goodTransactionMap);
            }
        }
		
        public void LoadTransactions(string filePath, Dictionary<string,List<TransactionInfo>> tempTransactions) {
            using( FileStream fileStream = new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
                StreamReader file = new StreamReader(fileStream);
                string line;
                while( (line = file.ReadLine()) != null) {
                    string[] fields = line.Split(',');
                    int fieldIndex = 0;
                    string strategyName = fields[fieldIndex++];
                    TransactionInfo testInfo = new TransactionInfo();
					
                    testInfo.Symbol = fields[fieldIndex++];
					
                    line = string.Join(",",fields,fieldIndex,fields.Length-fieldIndex);
                    testInfo.Fill = LogicalFillBinary.Parse(line);
                    List<TransactionInfo> transactionList;
                    if( tempTransactions.TryGetValue(strategyName,out transactionList)) {
                        transactionList.Add(testInfo);
                    } else {
                        transactionList = new List<TransactionInfo>();
                        transactionList.Add(testInfo);
                        tempTransactions.Add(strategyName,transactionList);
                    }
                }
            }
        }
		
        public void LoadReconciliation(string filePath, Dictionary<string,List<TransactionInfo>> tempReconciliation) {
            using( FileStream fileStream = new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
                StreamReader file = new StreamReader(fileStream);
                string line;
                int count = 0;
                while( (line = file.ReadLine()) != null) {
                    string[] fields = line.Split(',');
                    int fieldIndex = 0;
                    string strategyName = fields[fieldIndex++];
                    TransactionInfo testInfo = new TransactionInfo();
					
                    testInfo.Symbol = fields[fieldIndex++];
					
                    line = string.Join(",",fields,fieldIndex,fields.Length-fieldIndex);
                    testInfo.Fill = LogicalFillBinary.Parse(line);
                    List<TransactionInfo> transactionList;
                    if( tempReconciliation.TryGetValue(testInfo.Symbol,out transactionList)) {
                        transactionList.Add(testInfo);
                    } else {
                        transactionList = new List<TransactionInfo>();
                        transactionList.Add(testInfo);
                        tempReconciliation.Add(testInfo.Symbol,transactionList);
                    }
                    count++;
                }
                log.Warn( "Loaded "+ count + " transactions from " + filePath);
            }
        }
		
        public void LoadBarData() {
            string fileDir = @"..\..\Platform\ExamplesPluginTests\Loaders\Trades\";
            string newPath = Factory.SysLog.LogFolder + @"\BarData.log";
            string knownGoodPath = fileDir + testFileName + "BarData.log";
            if( File.Exists(newPath)) {
                if( storeKnownGood) {
                    File.Copy(newPath,knownGoodPath,true);
                }
                testBarDataMap.Clear();
                LoadBarData(newPath,testBarDataMap);
            }
            if( File.Exists(knownGoodPath)) {
                goodBarDataMap.Clear();
                LoadBarData(knownGoodPath,goodBarDataMap);
            }
        }
		
        public void LoadBarData(string filePath, Dictionary<string,List<BarInfo>> tempBarData) {
            using( FileStream fileStream = new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
                StreamReader file = new StreamReader(fileStream);
                string line;
                while( (line = file.ReadLine()) != null) {
                    string[] fields = line.Split(',');
                    int fieldIndex = 0;
                    string strategyName = fields[fieldIndex++];
                    BarInfo barInfo = new BarInfo();
					
                    barInfo.Time = new TimeStamp(fields[fieldIndex++]);
                    barInfo.EndTime = new TimeStamp(fields[fieldIndex++]);
                    barInfo.Open = double.Parse(fields[fieldIndex++]);
                    barInfo.High = double.Parse(fields[fieldIndex++]);
                    barInfo.Low = double.Parse(fields[fieldIndex++]);
                    barInfo.Close = double.Parse(fields[fieldIndex++]);
					
                    List<BarInfo> barList;
                    if( tempBarData.TryGetValue(strategyName,out barList)) {
                        barList.Add(barInfo);
                    } else {
                        barList = new List<BarInfo>();
                        barList.Add(barInfo);
                        tempBarData.Add(strategyName,barList);
                    }
                }
            }
        }
		
        public void LoadStats() {
            string fileDir = @"..\..\Platform\ExamplesPluginTests\Loaders\Trades\";
            string newPath = Factory.SysLog.LogFolder + @"\Stats.log";
            string knownGoodPath = fileDir + testFileName + "Stats.log";
            if( File.Exists(newPath)) {
                if( storeKnownGood) {
                    File.Copy(newPath,knownGoodPath,true);
                }
                testStatsMap.Clear();
                LoadStats(newPath,testStatsMap);
            }
            if( File.Exists(knownGoodPath)) {
                goodStatsMap.Clear();
                LoadStats(knownGoodPath,goodStatsMap);
            }
        }
		
        public void LoadStats(string filePath, Dictionary<string,List<StatsInfo>> tempStats) {
            using( FileStream fileStream = new FileStream(filePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
                StreamReader file = new StreamReader(fileStream);
                string line;
                while( (line = file.ReadLine()) != null) {
                    string[] fields = line.Split(',');
                    int fieldIndex = 0;
                    string strategyName = fields[fieldIndex++];
                    StatsInfo statsInfo = new StatsInfo();
                    statsInfo.Time = new TimeStamp(fields[fieldIndex++]);
                    statsInfo.ClosedEquity = double.Parse(fields[fieldIndex++]);
                    statsInfo.OpenEquity = double.Parse(fields[fieldIndex++]);
                    statsInfo.CurrentEquity = double.Parse(fields[fieldIndex++]);

                    List<StatsInfo> statsList;
                    if( tempStats.TryGetValue(strategyName,out statsList)) {
                        statsList.Add(statsInfo);
                    } else {
                        statsList = new List<StatsInfo>();
                        statsList.Add(statsInfo);
                        tempStats.Add(strategyName,statsList);
                    }
                }
            }
        }
		
        public void VerifyTradeCount(StrategyInterface strategy) {
            DynamicTradeCount(strategy.Name);
        }
		
        public void DynamicTradeCount(string strategyName) {
            try {
                if( string.IsNullOrEmpty(strategyName)) return;
                List<TradeInfo> goodTrades = null;
                goodTradeMap.TryGetValue(strategyName,out goodTrades);
                List<TradeInfo> testTrades = null;
                testTradeMap.TryGetValue(strategyName,out testTrades);
                if( goodTrades == null) {
                    Assert.IsNull(testTrades, "test trades empty like good trades");
                    return;
                }
                Assert.IsNotNull(testTrades, "test trades");
                Assert.AreEqual(goodTrades.Count,testTrades.Count,"trade count");
            }
            catch (AssertionException ex)
            {
                log.Error(ex.Message);
                testFailed = true;
                throw;
            }
            catch (IgnoreException)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                testFailed = true;
                throw;
            }
        }
		
        public void VerifyTransactionCount(StrategyInterface strategy) {
            List<TransactionInfo> goodTransactions = null;
            goodTransactionMap.TryGetValue(strategy.Name,out goodTransactions);
            List<TransactionInfo> testTransactions = null;
            testTransactionMap.TryGetValue(strategy.Name,out testTransactions);
            Assert.IsNotNull(goodTransactions, "good trades");
            Assert.IsNotNull(testTransactions, "test trades");
            Assert.AreEqual(goodTransactions.Count,testTransactions.Count,"transaction fill count");
        }
		
        public void VerifyBarDataCount(StrategyInterface strategy) {
            DynamicBarDataCount( strategy.Name);
        }
		
        public void DynamicBarDataCount(string strategyName) {
            try {
			    if( string.IsNullOrEmpty(strategyName)) return;
                List<BarInfo> goodBarData = null;
                try
                {
                    goodBarData = goodBarDataMap[strategyName];
                }
                catch
                {
                    if( ignoreMissingKnownGood)
                    {
                        Assert.Ignore();
                    }
                    else
                    {
                        throw;
                    }
                }
			    List<BarInfo> testBarData = testBarDataMap[strategyName];
			    Assert.AreEqual(goodBarData.Count,testBarData.Count,"bar data count for " + strategyName);
			}
            catch (AssertionException ex)
            {
                log.Error(ex.Message);
                testFailed = true;
                throw;
            }
            catch (IgnoreException)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                testFailed = true;
                throw;
            }
        }
		
        public void VerifyTrades(StrategyInterface strategy) {
            DynamicTrades(strategy.Name);
        }
		
        public void DynamicTrades(string strategyName) {
            if( string.IsNullOrEmpty(strategyName)) return;
            try {
                var assertFlag = false;
                List<TradeInfo> goodTrades = null;
                goodTradeMap.TryGetValue(strategyName,out goodTrades);
                List<TradeInfo> testTrades = null;
                testTradeMap.TryGetValue(strategyName,out testTrades);
                if( goodTrades == null) {
                    Assert.IsNull(testTrades, "test trades should be null because good was null.");
                    return;
                }
                Assert.IsNotNull(testTrades, "test trades");
                for( int i=0; i<testTrades.Count && i<goodTrades.Count; i++) {
                    TradeInfo testInfo = testTrades[i];
                    TradeInfo goodInfo = goodTrades[i];
                    TransactionPairBinary goodTrade = goodInfo.Trade;
                    TransactionPairBinary testTrade = testInfo.Trade;
                    AssertEqual(ref assertFlag, goodTrade,testTrade,strategyName + " Trade at " + i);
                    AssertEqual(ref assertFlag, goodInfo.ProfitLoss,testInfo.ProfitLoss,"ProfitLoss at " + i);
                    AssertEqual(ref assertFlag, goodInfo.ClosedEquity,testInfo.ClosedEquity,"ClosedEquity at " + i);
                }
                Assert.IsFalse(assertFlag,"Checking for trade errors.");
			}
            catch (AssertionException ex)
            {
                log.Error(ex.Message);
                testFailed = true;
                throw;
            }
            catch (IgnoreException)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                testFailed = true;
                throw;
            }
        }

        public void VerifyStatsCount(StrategyInterface strategy) {
            DynamicStatsCount( strategy.Name);
        }
		
        public void DynamicLatencyTest( string strategyName) {
            if( string.IsNullOrEmpty(strategyName)) return;
            if( autoTestMode == AutoTestMode.FIXPlayBack) {
                log.Info("Max Latency Test found: " + StaticGlobal.MaxLatency + "ms.");
                Assert.Less(StaticGlobal.MaxLatency,90,"max latency milliseconds");
            }
        }
		
        public void DynamicStatsCount(string strategyName) {
            try {
			    if( string.IsNullOrEmpty(strategyName)) return;
                List<StatsInfo> goodStats = null;
                try
                {
                    goodStats = goodStatsMap[strategyName];
                } catch
                {
                    if( ignoreMissingKnownGood)
                    {
                        Assert.Ignore();
                    }
                    else
                    {
                        throw;
                    }
                }
                List<StatsInfo> testStats = testStatsMap[strategyName];
			    Assert.AreEqual(goodStats.Count,testStats.Count,"Stats count for " + strategyName);
			}
            catch( AssertionException ex)
			{
			    log.Error(ex.Message);
				testFailed = true;
				throw;
			}
            catch (IgnoreException)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                testFailed = true;
                throw;
            }
        }
		
        private void AssertEqual(ref bool assertFlag, object a, object b, string message) {
            if( a.GetType() != b.GetType()) {
                throw new ApplicationException("Expected type " + a.GetType() + " but was " + b.GetType() + ": " + message);
            }
            if( a is double && b is double) {
                a = Math.Round((double)a ,2);
                b = Math.Round((double)b ,2);
            }
            if( !a.Equals(b)) {
                assertFlag = true;				
                log.Error("Mismatch:\nExpected '" + a + "'\n but was '" + b + "': " + message);
            }
        }
		
        private void AssertReconcile(ref bool assertFlag, LogicalFillBinary a, LogicalFillBinary b, string message) {
            if( a.GetType() != b.GetType()) {
                throw new ApplicationException("Expected type " + a.GetType() + " but was " + b.GetType() + ": " + message);
            }
            if( a.Position != b.Position || a.Price != b.Price || a.Time != b.Time) {
                assertFlag = true;
                log.Error("Expected '" + a + "' but was '" + b + "': " + message);
            }
        }
		
        public void VerifyFinalStats(StrategyInterface strategy) {
            DynamicFinalStats( strategy.Name);
        }
		
        public void DynamicFinalStats(string strategyName) {
            if( testName == "ExampleMixedTruePartial" && strategyName == "Portfolio")
            {
                Assert.Ignore();
            }
            try {
			    if( string.IsNullOrEmpty(strategyName)) return;
			    var assertFlag = false;
                FinalStatsInfo goodInfo = null;
                FinalStatsInfo testInfo = null;
		        try {
			        goodInfo = goodFinalStatsMap[strategyName];
                }
                catch
                {
                    if( ignoreMissingKnownGood)
                    {
                        Assert.Ignore();
                    }
                    else
                    {
                        throw;
                    }
                }
                testInfo = testFinalStatsMap[strategyName];
                AssertEqual(ref assertFlag, goodInfo.StartingEquity, testInfo.StartingEquity, strategyName + " Final Starting Equity");
			    AssertEqual(ref assertFlag, goodInfo.ClosedEquity,testInfo.ClosedEquity,strategyName + " Final Closed Equity");
                AssertEqual(ref assertFlag, goodInfo.OpenEquity, testInfo.OpenEquity, strategyName + " Final Open Equity");
                AssertEqual(ref assertFlag, goodInfo.CurrentEquity, testInfo.CurrentEquity, strategyName + " Final Current Equity");
			    Assert.IsFalse(assertFlag,"Checking for final statistics errors.");
		    }
            catch (AssertionException ex)
            {
                log.Error(ex.Message);
                testFailed = true;
                throw;
            }
            catch (IgnoreException)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                testFailed = true;
                throw;
            }
        }
		
        public void VerifyStats(StrategyInterface strategy) {
            DynamicStats( strategy.Name);
        }
		
        public void DynamicStats(string strategyName) {
            if (testName == "ExampleMixedTruePartial" && strategyName == "Portfolio")
            {
                Assert.Ignore();
            }
            if (string.IsNullOrEmpty(strategyName)) return;
			try
			{
			    List<StatsInfo> goodStats = null;
                try
                {
                    goodStats = goodStatsMap[strategyName];
                }
                catch
                {
                    if (ignoreMissingKnownGood)
                    {
                        Assert.Ignore();
                    }
                    else
                    {
                        throw;
                    }
    			}
				List<StatsInfo> testStats = testStatsMap[strategyName];
                var errorCount=0;
                for( int i=0; i<testStats.Count && i<goodStats.Count && errorCount<10; i++) {
                    StatsInfo testInfo = testStats[i];
                    StatsInfo goodInfo = goodStats[i];
                    //goodInfo.Time += realTimeOffset;
                    var assertFlag = false;
                    AssertEqual(ref assertFlag, goodInfo.Time,testInfo.Time,strategyName + " - [" + i + "] Stats time at " + testInfo.Time);
                    AssertEqual(ref assertFlag, goodInfo.ClosedEquity,testInfo.ClosedEquity,strategyName + " - [" + i + "] Closed Equity time at " + testInfo.Time);
                    AssertEqual(ref assertFlag, goodInfo.OpenEquity,testInfo.OpenEquity,strategyName + " - [" + i + "] Open Equity time at " + testInfo.Time);
                    AssertEqual(ref assertFlag, goodInfo.CurrentEquity,testInfo.CurrentEquity,strategyName + " - [" + i + "] Current Equity time at " + testInfo.Time);
                    if( assertFlag) {
                        errorCount++;
                    }
                }
                Assert.AreEqual(errorCount,0,"Checking for stats errors.");
            }
            catch (AssertionException ex)
            {
                log.Error(ex.Message);
                testFailed = true;
                throw;
            }
            catch (IgnoreException)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                testFailed = true;
                throw;
            }
        }
		
        public void VerifyBarData(StrategyInterface strategy) {
            DynamicBarData(strategy.Name);
        }
		
        public void DynamicBarData(string strategyName) {
            if( string.IsNullOrEmpty(strategyName)) return;
			try
			{
			    List<BarInfo> goodBarData = null;
                try
                {
                    goodBarData = goodBarDataMap[strategyName];
                }
                catch
                {
                    if( ignoreMissingKnownGood)
                    {
                        Assert.Ignore();
                    }
                    else
                    {
                        throw;
                    }
                }
                List<BarInfo> testBarData = testBarDataMap[strategyName];
                if( goodBarData == null) {
                    Assert.IsNull(testBarData, "test bar data matches good bar data");
                    return;
                }
                Assert.IsNotNull(testBarData, "test test data");
                var i=0;
                var errorCount = 0;
                for( ; i<testBarData.Count && i<goodBarData.Count && errorCount < 10; i++) {
                    BarInfo testInfo = testBarData[i];
                    BarInfo goodInfo = goodBarData[i];
                    //goodInfo.Time += realTimeOffset;
                    //goodInfo.EndTime += realTimeOffset;
                    var assertFlag = false;
                    AssertEqual(ref assertFlag, goodInfo.Time,testInfo.Time,strategyName + ": Time at bar " + i );
                    AssertEqual(ref assertFlag, goodInfo.EndTime, testInfo.EndTime,strategyName + ": End Time at bar " + i);
                    AssertEqual(ref assertFlag, goodInfo.Open,testInfo.Open,strategyName + ": Open at bar " + i + " " + testInfo.Time);
                    AssertEqual(ref assertFlag, goodInfo.High,testInfo.High,strategyName + ": High at bar " + i + " " + testInfo.Time);
                    AssertEqual(ref assertFlag, goodInfo.Low,testInfo.Low,strategyName + ": Low at bar " + i + " " + testInfo.Time);
                    AssertEqual(ref assertFlag, goodInfo.Close,testInfo.Close,strategyName + ": Close at bar " + i + " " + testInfo.Time);
                    if( assertFlag) {
                        errorCount++;
                    }
                }
                var extraTestBars = (testBarData.Count-i);
                if( extraTestBars > 0) {
                    log.Error( extraTestBars + " extra " + strategyName + " test bars. Listing first 10.");
                }
                for( var j=0; i<testBarData.Count && j<10; i++, j++) {
                    BarInfo testInfo = testBarData[i];
                    log.Error("Extra test bar: #"+i+" " + testInfo);
                    errorCount++;
                }
				
                var extraGoodBars = (goodBarData.Count-i);
                if( extraGoodBars > 0) {
                    log.Error( extraGoodBars + " extra " + strategyName + " good bars. Listing first 10.");
                }
                for( var j=0; i<goodBarData.Count && j<10; i++, j++) {
                    BarInfo goodInfo = goodBarData[i];
                    //goodInfo.Time += realTimeOffset;
                    log.Error("Extra good bar: #"+i+" " + goodInfo);
                    errorCount++;
                }
                Assert.AreEqual(errorCount,0,"Checking for bar data errors.");
            }
            catch (AssertionException ex)
            {
                log.Error(ex.Message);
                testFailed = true;
                throw;
            }
            catch (IgnoreException)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                testFailed = true;
                throw;
            }
        }
		
        public void VerifyPair(Strategy strategy, int pairNum,
                               string expectedEntryTime,
                               double expectedEntryPrice,
                               string expectedExitTime,
                               double expectedExitPrice)
        {
            try {
			    Assert.Greater(strategy.Performance.ComboTrades.Count, pairNum);
    		    TransactionPairs pairs = strategy.Performance.ComboTrades;
    		    TransactionPair pair = pairs[pairNum];
    		    TimeStamp expEntryTime = new TimeStamp(expectedEntryTime);
    		    Assert.AreEqual( expEntryTime, pair.EntryTime, "Pair " + pairNum + " Entry");
    		    Assert.AreEqual( expectedEntryPrice, pair.EntryPrice, "Pair " + pairNum + " Entry");
        		
    		    Assert.AreEqual( new TimeStamp(expectedExitTime), pair.ExitTime, "Pair " + pairNum + " Exit");
    		    Assert.AreEqual( expectedExitPrice, pair.ExitPrice, "Pair " + pairNum + " Exit");
        		
		    }
            catch (AssertionException ex)
            {
                log.Error(ex.Message);
                testFailed = true;
                throw;
            }
            catch (IgnoreException)
            {
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                testFailed = true;
                throw;
            }
        }
   		
        public void HistoricalShowChart()
        {
            log.DebugFormat("HistoricalShowChart() start.");
            if( ShowCharts) {
                try {
                    for( int i=portfolioDocs.Count-1; i>=0; i--) {
                        portfolioDocs[i].ShowInvoke();
                    }
                } catch( Exception ex) {
                    log.DebugFormat(ex.ToString());
                }
            }
        }
		
        public void HistoricalCloseCharts()
        {
            try {
                foreach( var doc in portfolioDocs) {
                    doc.Close();
                }
                portfolioDocs.Clear();
            } catch( Exception ex) {
                log.DebugFormat(ex.ToString());
            }
            log.DebugFormat("HistoricalShowChart() finished.");
        }
		
        public TickZoom.Api.Chart HistoricalCreateChart()
        {
            PortfolioDoc doc = null;
            try {
                execute.OnUIThreadSync( () => {
                                                  doc = new PortfolioDoc( execute);
                                                  portfolioDocs.Add(doc);
                });
                return doc.ChartControl;
            } catch( Exception ex) {
                log.Notice(ex.ToString());
            }
            return null;
        }
   		
        public int ChartCount {
            get { return portfolioDocs.Count; }
        }
   		
        protected ChartControl GetChart( string symbol) {
            ChartControl chart;
            for( int i=0; i<portfolioDocs.Count; i++) {
                chart = portfolioDocs[i].ChartControl;
                if( chart.Symbol.ExpandedSymbol == symbol) {
                    return chart;
                }
            }
            return null;
        }
   		
        public ChartControl GetChart(int i) {
            return portfolioDocs[i].ChartControl;
        }

        public string DataFolder {
            get { return dataFolder; }
            set { dataFolder = value; }
        }
		
        public void VerifyChartBarCount(string symbol, int expectedCount) {
            ChartControl chart = GetChart(symbol);
            GraphPane pane = chart.DataGraph.MasterPane.PaneList[0];
            Assert.IsNotNull(pane.CurveList);
            Assert.Greater(pane.CurveList.Count,0);
            Assert.AreEqual(symbol,chart.Symbol);
            Assert.AreEqual(expectedCount,chart.StockPointList.Count,"Stock point list");
            Assert.AreEqual(expectedCount,pane.CurveList[0].Points.Count,"Chart Curve");
        }
   		
        public void CompareChart(StrategyInterface strategy, ChartControl chart) {
            CompareChart( strategy);
        }
   		
        private ModelInterface GetModelByName(string modelName) {
            foreach( var model in GetAllModels(topModel)) {
                if( model.Name == modelName) {
                    return model;
                }
            }
            throw new ApplicationException("Model was not found for the name: " + modelName);
        }
   		
        public void CompareChart(StrategyInterface strategy) {
            execute.Flush();
            if( strategy.SymbolDefault == "TimeSync") return;
            var strategyBars = strategy.Bars;
            var chart = GetChart(strategy.SymbolDefault);
            var pane = chart.DataGraph.MasterPane.PaneList[0];
            Assert.IsNotNull(pane.CurveList);
            Assert.Greater(pane.CurveList.Count,0);
            var chartBars = (OHLCBarItem) pane.CurveList[0];
            int firstMisMatch = int.MaxValue;
            int i, j;
            for( i=0; i<strategyBars.Count; i++) {
                j=chartBars.NPts-i-1;
                if( j < 0 || j >= chartBars.NPts) {
                    log.DebugFormat("bar {0} is missing", i);
                } else {
                    StockPt bar = (StockPt) chartBars[j];
                    string match = "NOT match";
                    if( strategyBars.Open[i] == bar.Open &&
                        strategyBars.High[i] == bar.High &&
                        strategyBars.Low[i] == bar.Low &&
                        strategyBars.Close[i] == bar.Close) {
                            match = "matches";
                        } else {
                            if( firstMisMatch == int.MaxValue) {
                                firstMisMatch = i;
                            }
                        }
                    log.DebugFormat( "bar: {0}, point: {1} {2} days:{3},{4},{5},{6} => {7},{8},{9},{10}", i, j, match, strategyBars.Open[i], strategyBars.High[i], strategyBars.Low[i], strategyBars.Close[i], bar.Open, bar.High, bar.Low, bar.Close);
                    log.DebugFormat( "bar: {0}, point: {1} {2} days:{3} {4}", i, j, match, strategyBars.Time[i], new TimeStamp(bar.X));
                }
            }
            if( firstMisMatch != int.MaxValue) {
                i = firstMisMatch;
                j=chartBars.NPts-i-1;
                StockPt bar = (StockPt) chartBars[j];
                Assert.AreEqual(strategyBars.Open[i],bar.Open,"Open for bar " + i + ", point " + j);
                Assert.AreEqual(strategyBars.High[i],bar.High,"High for bar " + i + ", point " + j);
                Assert.AreEqual(strategyBars.Low[i],bar.Low,"Low for bar " + i + ", point " + j);
                Assert.AreEqual(strategyBars.Close[i],bar.Close,"Close for bar " + i + ", point " + j);
            }
        }
			
        public IEnumerable<string> GetModelNames()
        {
            ModelLoaderInterface loaderInstance = null;
            var result = false;
            try
            {
                var starter = SetupDesignStarter();
                loaderInstance = GetLoaderInstance();
                loaderInstance.OnInitialize(starter.ProjectProperties);
                loaderInstance.OnLoad(starter.ProjectProperties);
                result = true;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
            }
            if( result)
            {
                foreach (var model in GetAllModels(loaderInstance.TopModel))
                {
                    yield return model.Name;
                }
            }
        }

        public IEnumerable<SymbolInfo> GetSymbols() {
            var starter = SetupDesignStarter();
            foreach (var symbol in starter.ProjectProperties.Starter.SymbolInfo)
            {
                yield return symbol;
            }
        }

		
        public static IEnumerable<ModelInterface> GetAllModels( ModelInterface topModel) {
            yield return topModel;
            if( topModel is Portfolio) {
                foreach( var chainLink in topModel.Chain.Dependencies) {
                    var model = chainLink.Model;
                    foreach( var child in GetAllModels(model)) {
                        yield return child;
                    }
                }
            }
        }
		
        public void CompareChartCount(Strategy strategy) {
            ChartControl chart = GetChart(strategy.SymbolDefault);
            GraphPane pane = chart.DataGraph.MasterPane.PaneList[0];
            Assert.IsNotNull(pane.CurveList);
            Assert.Greater(pane.CurveList.Count,0);
            OHLCBarItem bars = (OHLCBarItem) pane.CurveList[0];
            Bars days = strategy.Days;
            Assert.AreEqual(strategy.SymbolDefault,chart.Symbol);
            Assert.AreEqual(days.BarCount,chart.StockPointList.Count,"Stock point list");
            Assert.AreEqual(days.BarCount,bars.NPts,"Chart Points");
        }
   		
        public string Symbols {
            get { return symbols; }
            set { symbols = value; }
        }
		
        public CreateStarterCallback CreateStarterCallback {
            get { return createStarterCallback; }
            set { createStarterCallback = value; }
        }
		
        public Interval IntervalDefault {
            get { return intervalDefault; }
            set { intervalDefault = value; }
        }		
		
        public TimeStamp StartTime {
            get { return startTime; }
            set { startTime = value; }
        }
		
        public TimeStamp EndTime {
            get { return endTime; }
            set { endTime = value; }
        }
		
        public AutoTestMode AutoTestMode {
            get { return autoTestMode; }
            set { autoTestMode = value; }
        }
    }
}