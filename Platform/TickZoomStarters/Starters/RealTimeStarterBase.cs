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
using System.IO;

using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Starters
{
	public class RealTimeStarterBase : StarterCommon
	{
		Log log = Factory.SysLog.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public override void Run(ModelInterface model)
		{
		    Factory.Provider.StartSockets();
            runMode = RunMode.RealTime;
            try
            {
                base.Run(model);
            }
            finally
            {
                parallelMode = ParallelMode.Normal;
                Factory.Provider.ShutdownSockets();
            }
		}

		public void SetupProviderServiceConfig()
		{
			try {
                var storageFolder = Factory.Settings["AppDataFolder"];
                var providersPath = Path.Combine(storageFolder, "Providers");
                var configPath = Path.Combine(providersPath, "ProviderService");
                var configFile = Path.Combine(configPath, "WarehouseTest.config");
                var warehouseConfig = new ConfigFile(configFile);
                warehouseConfig.SetValue("ServerCacheFolder", "Test\\ServerCache");
                var dataProvider = DataProviders[0];
                warehouseConfig.SetValue("ActiveAccounts", "default");
                warehouseConfig.SetValue("DataProvider", dataProvider);
                var executionProvider = ExecutionProviders[0];
                warehouseConfig.SetValue("default/ExecutionProvider", executionProvider);
            }
            catch (Exception ex)
            {
				log.Error("Setup error.",ex);
				throw ex;
			}
		}
	}
}
