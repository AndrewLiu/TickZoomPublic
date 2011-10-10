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
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using log4net.Appender;
using log4net.Core;
using log4net.Filter;
using log4net.Repository;
using TickZoom.Api;

namespace TickZoom.Logging
{
	public class LogManagerImpl : LogManager {
		private static Log exceptionLog;
		private static object locker = new object();
		private ILoggerRepository repository;
		private string repositoryName;
        private string currentExtension;
        Dictionary<string, LogImpl> map = new Dictionary<string, LogImpl>();

		public void ConfigureSysLog() {
			this.repositoryName = "SysLog";
			this.repository = LoggerManager.CreateRepository(repositoryName);
			Reconfigure(null,GetSysLogDefault());
		}

        public void ConfigureUserLog()
        {
            Console.Out.Write("UserLog from\n" + Environment.StackTrace);
            this.repositoryName = "Log";
            this.repository = LoggerManager.CreateRepository(repositoryName);
            Reconfigure(null, GetLogDefault());
        }

        public void ResetConfiguration()
        {
			Reconfigure(null,null);
		}

	    private string realTimeConfig;
	    private string realTimeDefaultConfig;
        private string historicalConfig;
        private string historicalDefaultConfig;

	    public void RegisterRealTime(string configName, string defaultConfig)
	    {
	        realTimeConfig = configName;
	        realTimeDefaultConfig = defaultConfig;
	    }

        public void RegisterHistorical(string configName, string defaultConfig)
        {
            historicalConfig = configName;
            historicalDefaultConfig = defaultConfig;
            Reconfigure( historicalConfig, historicalDefaultConfig);
        }

        public void ReconfigureForHistorical()
        {
            if( historicalConfig == null || historicalDefaultConfig == null)
            {
                throw new ApplicationException("Please call RegisterHistorical() before calling ReconfigureHistorical().");
            }
            Reconfigure(historicalConfig, historicalDefaultConfig);
        }

        public void ReconfigureForRealTime()
        {
            if( realTimeConfig == null || realTimeDefaultConfig == null)
            {
                throw new ApplicationException("Please call RegisterRealTime() before calling ReconfigureRealTime().");
            }
            Reconfigure(realTimeConfig, realTimeDefaultConfig);
        }

        public void Reconfigure(string extension)
        {
            if( extension == null)
            {
                throw new InvalidOperationException("Parameter cannot be null.");
            }
            if (extension == repositoryName)
            {
                extension = null;
            }
            extension = extension.Replace(repositoryName + ".", "");
            Reconfigure(extension, null);
        }

	    private void Reconfigure(string extension, string defaultConfig)
        {
            this.currentExtension = extension;
			if( repositoryName == "SysLog" && ConfigurationManager.GetSection("log4net") != null) {
				log4net.Config.XmlConfigurator.Configure(repository);
			} else {
				var xml = GetConfigXML(repositoryName,extension,defaultConfig);
				repository.ResetConfiguration();
				log4net.Config.XmlConfigurator.Configure(repository,xml);
			}
			lock( locker) {
				if( exceptionLog == null) {
					exceptionLog = GetLogger("TickZoom.AppDomain");
				}
			}
		}

        private XmlElement GetConfigXML(string repositoryName, string extension, string defaultConfig)
        {
			var configBase = VerifyConfigPath(repositoryName, defaultConfig);
			var xmlbase = File.OpenText(configBase);
			var doc1 = new XmlDocument();
			doc1.LoadXml(xmlbase.ReadToEnd());
			var doc1Configs = doc1.GetElementsByTagName("log4net");
			if( doc1Configs.Count > 1) {
				throw new ApplicationException("Can't have more than one log4net element.");
			}
			if( doc1Configs.Count == 0) {
				throw new ApplicationException("Must have an log4net element.");
			}
			var doc1Config = doc1Configs[0];
			
			if( extension != null) {
				var extensionFile = VerifyConfigPath(repositoryName+"."+extension,defaultConfig);
				var xmlextension = File.OpenText(extensionFile);
				var doc2 = new XmlDocument();
				doc2.LoadXml(xmlextension.ReadToEnd());
				var doc2Configs = doc2.GetElementsByTagName("log4net");
				if( doc2Configs.Count > 1) {
					throw new ApplicationException("Can't have more than one log4net element.");
				}
				if( doc2Configs.Count == 0) {
					throw new ApplicationException("Must have an log4net element.");
				}
				var doc2Config = doc2Configs[0];
				foreach( var child in doc2Config) {
					var node = doc1.ImportNode((XmlNode)child,true);
					if( node.Name == "root") {
						var doc1Roots = doc1Config.SelectNodes("root");
						if( doc1Roots.Count == 1) {
							doc1Config.ReplaceChild(node,doc1Roots[0]);
						} else {
							throw new ApplicationException("Most have exactly 1 root element in the base config file: " + extensionFile);
						}
					} else if( node.Name == "logger") {
						doc1Config.AppendChild(node);
					} else {
						throw new ApplicationException("Only logger or root elements can be defined log configuration files: " + extensionFile);
					}
				}
			}
	
			return (XmlElement) doc1Config;
		}
		
		private string VerifyConfigPath(string repositoryName, string defaultConfig) {
			var path = GetConfigPath(repositoryName);
			if( !File.Exists(path)) {
                if( defaultConfig == null)
                {
                    throw new ApplicationException("Logging config " + repositoryName + " requested but " + path + " is not found and default config was null.");
                }
				File.WriteAllText(path,defaultConfig);
			}
			return path;
		}
		
		private string GetConfigPath(string repositoryName) {
			var storageFolder = Factory.Settings["AppDataFolder"];
			var configPath = Path.Combine(storageFolder,"Config");
			Directory.CreateDirectory(configPath);
			var configFile = Path.Combine(configPath,repositoryName+".config");
			return configFile;
		}

        public void Flush()
        {
            foreach( var appender in repository.GetAppenders())
            {
                var buffer = appender as BufferingAppenderSkeleton;
                if( buffer != null)
                {
                    buffer.Flush();
                }
            }
        }

        public List<string> GetConfigNames()
        {
            var storageFolder = Factory.Settings["AppDataFolder"];
            var configPath = Path.Combine(storageFolder, "Config");
            Directory.CreateDirectory(configPath);
            var configFiles = new List<string>();
            configFiles.AddRange(Directory.GetFiles(configPath, repositoryName + ".*.config", SearchOption.TopDirectoryOnly));
            configFiles.AddRange(Directory.GetFiles(configPath, repositoryName + ".config", SearchOption.TopDirectoryOnly));
            var results = new List<string>();
            foreach( var file in configFiles)
            {
                var configName = Path.GetFileNameWithoutExtension(file);
                results.Add(configName);
            }
            return results;
        }

	    public string ActiveConfigName
	    {
	        get { return repositoryName + (currentExtension == null ? "" : "." + currentExtension); }
	    }

        private static void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
		    Exception ex = (Exception)e.ExceptionObject;
		    exceptionLog.Error("Unhandled exception caught",ex);
		}
	
		public string LogFolder {
			get {
                // get the log directory
			    var logDirectory = Environment.GetEnvironmentVariable("AppLogFolder");
                if (logDirectory == null)
                {
                    logDirectory = Factory.Settings["AppDataFolder"];
                    logDirectory = Path.Combine(logDirectory, "Logs");
                }
				return logDirectory;
			}
		}
		
		public Log GetLogger(Type type) {
			LogImpl log;
			if( map.TryGetValue(type.FullName, out log)) {
				return log;
			} else {
				ILogger logger = repository.GetLogger(type.FullName);
				log = new LogImpl(logger);
				map[type.FullName] = log;
			}
			return log;
		}
		public Log GetLogger(string name) {
			LogImpl log;
			if( map.TryGetValue(name, out log)) {
				return log;
			} else {
				ILogger logger = repository.GetLogger(name);
				log = new LogImpl(logger);
				map[name] = log;
			}
			return log;
		}
		
		public string GetSysLogDefault() {
			return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
 <log4net>
 	<appender name=""StatsLogAppender"" type=""TickZoom.Logging.RollingFileAppender"">
	    <rollingStyle value=""Size"" />
	    <maxSizeRollBackups value=""10"" />
	    <maximumFileSize value=""100MB"" />
		<file value=""LogFolder\Stats.log"" />
		<appendToFile value=""false"" />
		<layout type=""log4net.Layout.PatternLayout"">
			<conversionPattern value=""%message%newline"" />
		</layout>
 	</appender>
 	<appender name=""BarDataLogAppender"" type=""TickZoom.Logging.RollingFileAppender"">
	    <rollingStyle value=""Size"" />
	    <maxSizeRollBackups value=""10"" />
	    <maximumFileSize value=""100MB"" />
		<file value=""LogFolder\BarData.log"" />
		<appendToFile value=""false"" />
		<layout type=""log4net.Layout.PatternLayout"">
			<conversionPattern value=""%message%newline"" />
		</layout>
 	</appender>
 	<appender name=""TradeLogAppender"" type=""TickZoom.Logging.RollingFileAppender"">
	    <rollingStyle value=""Size"" />
	    <maxSizeRollBackups value=""10"" />
	    <maximumFileSize value=""100MB"" />
		<file value=""LogFolder\Trades.log"" />
		<appendToFile value=""false"" />
		<layout type=""log4net.Layout.PatternLayout"">
			<conversionPattern value=""%message%newline"" />
		</layout>
 	</appender>
 	<appender name=""TransactionLogAppender"" type=""TickZoom.Logging.RollingFileAppender"">
	    <rollingStyle value=""Size"" />
	    <maxSizeRollBackups value=""10"" />
	    <maximumFileSize value=""100MB"" />
		<file value=""LogFolder\Transactions.log"" />
		<appendToFile value=""false"" />
    	<layout type=""log4net.Layout.PatternLayout"">
			<conversionPattern value=""%message%newline"" />
		</layout>
 	</appender>
	<appender name=""ConsoleAppender"" type=""log4net.Appender.ConsoleAppender"" >
 		<threshold value=""WARN""/>
		<layout type=""log4net.Layout.PatternLayout"">
			<conversionPattern value=""%date %-5level %logger %property{Symbol} %property{TimeStamp} - %message%newline"" />
		</layout>
 	</appender>
	<appender name=""FileAppender"" type=""TickZoom.Logging.RollingFileAppender"" >
	    <rollingStyle value=""Size"" />
	    <maxSizeRollBackups value=""100"" />
	    <maximumFileSize value=""100MB"" />
		<appendToFile value=""false"" />
		<file value=""LogFolder\TickZoom.log"" />
		<layout type=""log4net.Layout.PatternLayout"">
			<conversionPattern value=""%date %-5level %logger - %message%newline"" />
		</layout>
	</appender>
 	<root>
		<level value=""INFO"" />
		<appender-ref ref=""FileAppender"" />
		<appender-ref ref=""ConsoleAppender"" />
	</root>
 </log4net>
</configuration>
";
		}
		
		public string GetLogDefault() {
			return @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
 <log4net>
	<appender name=""FileAppender"" type=""TickZoom.Logging.RollingFileAppender"" >
	    <rollingStyle value=""Size"" />
	    <maxSizeRollBackups value=""100"" />
	    <maximumFileSize value=""100MB"" />
		<appendToFile value=""false"" />
		<file value=""LogFolder\User.log"" />
		<layout type=""log4net.Layout.PatternLayout"">
			<conversionPattern value=""%date %-5level %logger - %message%newline"" />
		</layout>
	</appender>
	<root>
		<level value=""INFO"" />
		<appender-ref ref=""FileAppender"" />
	</root>
 </log4net>
</configuration>
";
		}

	}
}
