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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using TickZoom.Api;

namespace TickZoom.Starters
{
    public class FIXSimulatorStarter : RealTimeStarterBase
    {
        private static readonly Log log = Factory.SysLog.GetLogger(typeof (FIXSimulatorStarter));
        private Dictionary<string,string> executionProviders = new Dictionary<string,string>();
        private Dictionary<string, string> dataProviders = new Dictionary<string, string>();

        public FIXSimulatorStarter()
        {
			SyncTicks.Enabled = true;
			ConfigurationManager.AppSettings.Set("ProviderAddress","InProcess");
        }
		
		public override void Run(ModelLoaderInterface loader)
		{
            executionProviders.Clear();
            dataProviders.Clear();
            var stopwatch = new Stopwatch();
		    stopwatch.Start();
            var elapsed = stopwatch.Elapsed;
#if !USE_MBT
            executionProviders.Add("default", "MBTFIXProvider/Simulate");
            dataProviders.Add("lime", "MBTFIXProvider/Simulate");
            var fixAssembly = "MBTFIXProvider";
            var fixSimulator = "ProviderSimulator";
#else
            executionProviders.Add("default", "LimeProvider/Simulate");
            dataProviders.Add("lime", "LimeProvider/Simulate");
            var fixAssembly = "LimeProvider";
            var fixSimulator = "ProviderSimulator";
#endif
            SetupSymbolData();
            log.Debug("SetupSymbolData took " + elapsed.TotalSeconds + " seconds and " + elapsed.Milliseconds + " milliseconds");
            stopwatch.Reset();
            stopwatch.Start();
		    Factory.Provider.StartSockets();
            parallelMode = ParallelMode.RealTime;
            Factory.SysLog.RegisterHistorical("FIXSimulator", GetDefaultLogConfig());
            Factory.SysLog.RegisterRealTime("FIXSimulator", GetDefaultLogConfig());
            Config = "WarehouseTest.config";
		    Address = "inprocess";
            SetupProviderServiceConfig();
            var providerManager = Factory.Parallel.SpawnProvider("ProviderCommon", "ProviderManager");
            providerManager.SendEvent(new EventItem(EventType.SetConfig, "WarehouseTest"));
            using (Factory.Parallel.SpawnProvider(fixAssembly, fixSimulator, "Simulate", ProjectProperties))
            {
                elapsed = stopwatch.Elapsed;
                log.Debug("Startup took " + elapsed.TotalSeconds + " seconds and " + elapsed.Milliseconds + " milliseconds");
                base.Run(loader);
			}
            Factory.Provider.ShutdownSockets();
        }

        public void SetupSymbolData()
        {
            string appDataFolder = Factory.Settings["AppDataFolder"];
            var realTimeDirectory = appDataFolder + Path.DirectorySeparatorChar +
                            "Test" + Path.DirectorySeparatorChar +
                            "MockProviderData";
            var historicalDirectory = appDataFolder + Path.DirectorySeparatorChar +
                            "Test" + Path.DirectorySeparatorChar +
                            "ServerCache";
            DeleteDirectory(realTimeDirectory);
            DeleteDirectory(historicalDirectory);
            Directory.CreateDirectory(realTimeDirectory);
            Directory.CreateDirectory(historicalDirectory);
            foreach (var symbol in ProjectProperties.Starter.SymbolInfo)
            {
                CopySymbol(historicalDirectory,realTimeDirectory,symbol.BaseSymbol);
            }
        }

        public static void DeleteDirectory(string path)
        {
            var errors = new List<Exception>();
            var errorCount = 0;
            while (errorCount < 30)
            {
                try
                {
                    if( Directory.Exists(path))
                    {
                        Directory.Delete(path,true);
                    }
                    errors.Clear();
                    break;
                }
                catch (Exception ex)
                {
                    log.Info("Delete " + path + " error " + errorCount + ": " + ex.Message);
                    errors.Add(ex);
                    Thread.Sleep(1000);
                    errorCount++;
                }
            }
            if (errors.Count > 0)
            {
                var ex = errors[errors.Count - 1];
                throw new IOException("Can't delete " + path, ex);
            }
        }

        public void SetupProviderServiceConfig()
        {
            try
            {
                var storageFolder = Factory.Settings["AppDataFolder"];
                var providersPath = Path.Combine(storageFolder, "Providers");
                var configPath = Path.Combine(providersPath, "ProviderService");
                var configFile = Path.Combine(configPath, "WarehouseTest.config");
                var warehouseConfig = new ConfigFile(configFile);
                warehouseConfig.SetValue("ServerCacheFolder", "Test\\ServerCache");
                var activeAccounts = "";
                foreach (var kvp in executionProviders)
                {
                    var account = kvp.Key;
                    if( activeAccounts.Length > 0)
                    {
                        activeAccounts += ",";
                    }
                    activeAccounts += account;
                }

                var dataSources = "";
                foreach (var kvp in dataProviders)
                {
                    var source = kvp.Key;
                    if (dataSources.Length > 0)
                    {
                        dataSources += ",";
                    }
                    dataSources += source;
                }

                warehouseConfig.SetValue("ActiveAccounts", activeAccounts);
                warehouseConfig.SetValue("DataSources", dataSources);
                foreach( var kvp in executionProviders)
                {
                    var account = kvp.Key;
                    var executionProvider = kvp.Value;
                    warehouseConfig.SetValue(account+"/ExecutionProvider", executionProvider);
                }
                foreach (var kvp in dataProviders)
                {
                    var source = kvp.Key;
                    var dataProvider = kvp.Value;
                    warehouseConfig.SetValue(source + "/DataProvider", dataProvider);
            }
            }
            catch (Exception ex)
            {
                log.Error("Setup error.", ex);
                throw ex;
            }
        }

        public void CopySymbol(string historical, string realTime, string symbol)
        {
            while (true)
            {
                try
                {
                    symbol = symbol.StripInvalidPathChars();
                    string appData = Factory.Settings["AppDataFolder"];
                    var fromDirectory = appData + Path.DirectorySeparatorChar +
                                    "Test" + Path.DirectorySeparatorChar +
                                    "DataCache";
                    var files = Directory.GetFiles(fromDirectory, symbol + ".tck", SearchOption.AllDirectories);
                    if( files.Length > 1)
                    {
                        var sb = new StringBuilder();
                        foreach (var file in files)
                        {
                            sb.AppendLine(file);
                        }
                        throw new ApplicationException("Sorry more than one file matches " + symbol + ".tck:\n" + sb);
                    }
                    else if( files.Length == 1)
                    {
                        var fromFile = files[0];
                        var symbolAndSource = symbol;
                        var defaultDataSource = "default";
                        foreach( var kvp in dataProviders)
                        {
                            // First data source is the default.
                            defaultDataSource = kvp.Key;
                            break;
                        }
                        if( defaultDataSource != "default")
                        {
                            symbolAndSource += Symbol.SourceSeparator + defaultDataSource;
                        }
                        var mockProviderFile = realTime + Path.DirectorySeparatorChar + symbol + ".tck";
                        var serverCacheFile = historical + Path.DirectorySeparatorChar + symbolAndSource + ".tck";
                        if( ProjectProperties.Simulator.WarmStartTime < ProjectProperties.Starter.EndTime)
                        {
                            SplitAndCopy(fromFile, serverCacheFile, mockProviderFile, ProjectProperties.Simulator.WarmStartTime);
                        }
                        else
                        {
                            File.Copy(fromFile, mockProviderFile);
                        }
                    }
                    break;
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Copy " + symbol + " to simulator failed. Retrying...", ex);
                }
            }
            Thread.Sleep(2000);
        }

        private void SplitAndCopy(string fromFile, string historyFile, string realTimeFile, TimeStamp cutoverTime)
        {
            // Setup historical
            var subproc = Factory.Provider.SubProcess();
            subproc.ExecutableName = "tzdata.exe";
            subproc.AddArgument("filter");
            subproc.AddArgument(fromFile);
            subproc.AddArgument(historyFile);
            subproc.AddArgument(ProjectProperties.Starter.StartTime.ToString());
            subproc.AddArgument(cutoverTime.ToString());
            subproc.Run();

            // Setup real time
            subproc = Factory.Provider.SubProcess();
            subproc.ExecutableName = "tzdata.exe";
            subproc.AddArgument("filter");
            subproc.AddArgument(fromFile);
            subproc.AddArgument(realTimeFile);
            subproc.AddArgument(cutoverTime.ToString());
            subproc.AddArgument(ProjectProperties.Starter.EndTime.ToString());
            subproc.Run();
        }

        private string GetDefaultLogConfig()
        {
			return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
 <log4net>
    <root>
	<level value=""INFO"" />
	<appender-ref ref=""FileAppender"" />
	<appender-ref ref=""ConsoleAppender"" />
    </root>
    <logger name=""StatsLog"">
        <level value=""INFO"" />
    	<additivity value=""false"" />
	<appender-ref ref=""StatsLogAppender"" />
    </logger>
    <logger name=""TradeLog"">
        <level value=""INFO"" />
    	<additivity value=""false"" />
	<appender-ref ref=""TradeLogAppender"" />
    </logger>
    <logger name=""TransactionLog.Performance"">
        <level value=""INFO"" />
    	<additivity value=""false"" />
	<appender-ref ref=""TransactionLogAppender"" />
    </logger>
    <logger name=""BarDataLog"">
        <level value=""INFO"" />
    	<additivity value=""false"" />
	<appender-ref ref=""BarDataLogAppender"" />
    </logger>
    <logger name=""TickZoom.Common"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.FIX"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.MBTFIX"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.MBTQuotes"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.Engine.SymbolReceiver"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.ProviderService"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.Engine.EngineKernel"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.Internals.OrderGroup"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.Internals.OrderManager"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.Engine.SymbolController"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.Interceptors.FillSimulatorPhysical"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.Interceptors.FillHandlerDefault"">
        <level value=""INFO"" />
    </logger>
    <logger name=""TickZoom.Common.OrderAlgorithmDefault"">
        <level value=""INFO"" />
    </logger>
 </log4net>
</configuration>
";				
		}

    }
}