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
using System.Diagnostics;
using System.IO;
using System.Management;

using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Starters
{
	/// <summary>
	/// Description of Test.
	/// </summary>
	public class OptimizeStarter : StarterCommon
	{
		Log log = Factory.SysLog.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		int totalTasks=0;
		ModelLoaderInterface loader;
		int tasksRemaining;
		long startMillis;
		int passCount = 0;
		
		public OptimizeStarter() {
		}
		
		public override void Run(ModelInterface model)
		{
			throw new MustUseLoaderException("Must set ModelLoader instead of Model for Optimization");
		}
		
		List<TickEngine> engineIterations;
		public override void Run(ModelLoaderInterface loader)
		{
            Factory.Parallel.SetMode(parallelMode);
            Factory.SysLog.RegisterHistorical("Optimize", HistoricalStarter.GetDefaultLogConfig());
			try {
				if( loader.OptimizeOutput == null) {
					Directory.CreateDirectory( Path.GetDirectoryName(FileName));
					File.Delete(FileName);
				}
			} catch( Exception ex) {
				log.Error("Error while creating directory and deleting '" + FileName + "'.",ex);
				return;
			}
			this.loader = loader;
			this.loader.QuietMode = true;
			startMillis = Factory.TickCount;
			engineIterations = new List<TickEngine>();
			
			loader.OnInitialize(ProjectProperties);
			
			totalTasks = 0;
			
			foreach( var num in RecursiveOptimize(0)) {
				totalTasks++;
			}
			
			tasksRemaining = totalTasks;
			
			int tasksPerEngine = CalculateTasksPerEngine(totalTasks);
			int totalEngineCount = totalTasks / tasksPerEngine;
			int iterations = Math.Max(1,totalTasks / maxParallelPasses);
			int leftOverPasses = Math.Max(0,totalTasks - (maxParallelPasses * iterations));
			if( totalTasks % maxParallelPasses > 0) {
				log.Notice("Planning " + iterations + " iterations with " + maxParallelPasses + " passes plus 1 pass with " + leftOverPasses + " iterations.");
				totalEngineCount ++;
			} else {
				log.Notice("Planning " + iterations + " iterations with " + maxParallelPasses + " passes each.");
			}
			
			var engines = new Stack<TickEngine>();
			for( int i=0; i<totalEngineCount; i++) {
				engines.Push( SetupEngine( true));
			}
			
			ModelInterface topModel = new Portfolio();

			passCount = 0;
			foreach (var num in RecursiveOptimize(0))
			{
				ModelInterface model = ProcessLoader(loader,passCount);
				topModel.Chain.Dependencies.Add(model.Chain);
				passCount++;
				if (passCount % tasksPerEngine == 0)
				{
					var engine = engines.Pop();
					engine.Model = topModel;
					engine.QueueTask();
					engineIterations.Add(engine);
					topModel = new Portfolio();
					if (engineIterations.Count >= Environment.ProcessorCount) {
						ProcessIteration();
					}
				}
			}

			if (topModel.Chain.Dependencies.Count > 0)
			{
				TickEngine engine = ProcessHistorical(topModel, true);
				engine.QueueTask();
				engineIterations.Add(engine);
			}
			
			if( engineIterations.Count > 0) {
				ProcessIteration();
			}
			
			long elapsedMillis = Factory.TickCount - startMillis;
			log.Notice("Finished optimizing in " + elapsedMillis + "ms.");
		}

		public void ProcessIteration() {

			GetEngineResults();
			
			WriteEngineResults(loader,engineIterations);
			
			foreach( var engine in engineIterations) {
				engine.Release();
			}

			engineIterations.Clear();
		}
		
		public override void Wait() {
			// finishes during Run()
		}
		
		private void GetEngineResults() {
			for( int i=0; i<engineIterations.Count; i++) {
				TickEngine engine = engineIterations[i];
				engine.WaitTask();
				--tasksRemaining;
			}
		}

		private bool CancelPending {
			get { if( BackgroundWorker != null) {
					return BackgroundWorker.CancellationPending;
				} else {
					return false;
				}
			}
		}
		
		private IEnumerable<int> RecursiveOptimize(int index) {
			if( index < loader.Variables.Count) {
				// Loop through a specific optimization variable.
				ModelProperty variable = loader.Variables[index];
				if( variable.Optimize) {
					for( double i = variable.Start;
					    i <= variable.End;
					    i = Math.Round(i+variable.Increment,9)) {
						variable.Value = i.ToString();
						foreach( var num in RecursiveOptimize(index+1)) {
							yield return num;
						}
					}
				} else {
					yield return 1;
				}
			} else {
				yield return 1;
			}
		}

		/// <summary>
		/// Returns available memory in megabytes.
		/// </summary>
		/// <returns></returns>
		private int GetAvailableMemory() {
			ObjectQuery winQuery = new ObjectQuery("SELECT * FROM Win32_LogicalMemoryConfiguration");
			ManagementObjectSearcher searcher = new ManagementObjectSearcher(winQuery);
			int memory = 0;
			foreach (ManagementObject item in searcher.Get())
			{
				UInt32 mem = (UInt32) item["AvailableVirtualMemory"];
				memory += (int) mem;
			}
			return memory / 1024;
		}
		
		/// <summary>
		/// Returns used memory in megabytes.
		/// </summary>
		/// <returns></returns>
		private int GetUsedMemory() {
			Process currentProcess = Process.GetCurrentProcess();
			long megabyte = 1024L * 1024L;
			int result = (int) (currentProcess.WorkingSet64 / megabyte);
			return result;
		}
	}
}