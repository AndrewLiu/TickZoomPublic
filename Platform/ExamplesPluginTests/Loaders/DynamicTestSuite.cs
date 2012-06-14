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
using System.Linq;
using NUnit.Core;
using NUnit.Core.Builders;
using NUnit.Core.Extensibility;
using TickZoom.Api;

namespace Loaders
{
	[SuiteBuilder]
	public class DynamicTestSuite : ISuiteBuilder
	{
		Type userFixtureType = typeof(StrategyBaseTest);
		private static readonly Log log = Factory.SysLog.GetLogger(typeof(DynamicTestSuite));
		public Test BuildFrom(Type type)
		{
			var autoTestFixture = (IAutoTestFixture) Reflect.Construct(type);
			var mainSuite = new TestSuite("DynamicTest");
			var modesToRun = autoTestFixture.GetModesToRun();
            var testModes = Enum.GetValues(typeof(AutoTestMode)).Cast<AutoTestMode>();
            foreach( var testMode in testModes)
            {
                bool singleBit = testMode > 0 && (testMode & (testMode - 1)) == 0;
                if (!singleBit) continue;
                if ((modesToRun & testMode) == testMode)
                {
                    AddDynamicTestFixtures(mainSuite, autoTestFixture, testMode);
                }
            }
			return mainSuite;
		}
		
		private void AddDynamicTestFixtures(TestSuite mainSuite, IAutoTestFixture autoTestFixture, AutoTestMode autoTestMode) {
			var suite = new TestSuite(autoTestMode.ToString());
			mainSuite.Add(suite);
			foreach( var testSettings in autoTestFixture.GetAutoTestSettings() ) {
				if( (testSettings.Mode & autoTestMode) != autoTestMode) continue;
				testSettings.Mode = autoTestMode;
				var fixture = new NUnitTestFixture(userFixtureType, new object[] { testSettings.Name, testSettings } );
				foreach( var category in testSettings.Categories) {
					fixture.Categories.Add( category);
				}
                var testModes = Enum.GetValues(typeof(AutoTestMode)).Cast<AutoTestMode>();
                foreach (var testMode in testModes)
                {
                    if ((autoTestMode & testMode) > 0)
                    {
                        fixture.Categories.Add(testMode.ToString());
                    }
                }
        		fixture.TestName.Name = testSettings.Name;
				suite.Add(fixture);
				AddStrategyTestCases(fixture, testSettings);
			}
		}
			
		private void AddStrategyTestCases(NUnitTestFixture fixture, AutoTestSettings testSettings) {
			var strategyTest = (StrategyBaseTest) Reflect.Construct(userFixtureType, new object[] { fixture.TestName.Name, testSettings } );
			foreach( var modelName in strategyTest.GetModelNames()) {
				var paramaterizedTest = new ParameterizedMethodSuite(modelName);
				foreach( var category in testSettings.Categories) {
					paramaterizedTest.Categories.Add( category);
				}
				fixture.Add(paramaterizedTest);
				var parms = new ParameterSet();
				parms.Arguments = new object[] { modelName };
				var methods = strategyTest.GetType().GetMethods();
				foreach( var method in methods ) {
					var parameters = method.GetParameters();
					if( !method.IsSpecialName && method.IsPublic && !method.IsStatic && parameters.Length == 1 && parameters[0].ParameterType == typeof(string)) {
						if( CheckIgnoreMethod(testSettings.IgnoreTests, method.Name)) {
							continue;
						}
						var testCase = NUnitTestCaseBuilder.BuildSingleTestMethod(method,parms);
						testCase.TestName.Name = method.Name;
						testCase.TestName.FullName = fixture.Parent.Parent.TestName.Name + "." +
							fixture.Parent.TestName.Name + "." + 
							fixture.TestName.Name + "." +
							modelName + "." +
							method.Name;
						paramaterizedTest.Add( testCase);
					}
				}
			}
		}
	
		private bool CheckIgnoreMethod(TestType ignoreTests, string methodName) {
        	var testTypeValues = Enum.GetValues(typeof(TestType));
	        foreach (TestType testType in testTypeValues)
	        {
	        	if ((ignoreTests & testType) == testType)
	            {
	        		if( methodName.Contains( testType.ToString())) {
	            	   	return true;
	            	}
	            }
	        }
	        return false;
		}
				           
		public bool CanBuildFrom(Type type)
		{
			var result = false;
			if( Reflect.HasAttribute( type, typeof(AutoTestFixtureAttribute).FullName, false) 
			   && Reflect.HasInterface( type, typeof(IAutoTestFixture).FullName) ) {
				var autoTestFixture = (IAutoTestFixture) Reflect.Construct(type);
                var testModes = Enum.GetValues(typeof(AutoTestMode)).Cast<AutoTestMode>();
                foreach (var testMode in testModes)
                {
                    bool singleBit = testMode > 0 && (testMode & (testMode - 1)) == 0;
                    if (!singleBit) continue;
                    if (CheckCanBuild(autoTestFixture, testMode))
                    {
                        result = true;
                        break;
                    }
                }
			}
			return result;
		}		
		
		private bool CheckCanBuild(IAutoTestFixture autoTestFixture, AutoTestMode testMode) {
			var result = false;
            bool singleBit = testMode > 0 && (testMode & (testMode - 1)) == 0;
            if (!singleBit) return false;
			foreach( var testSettings in autoTestFixture.GetAutoTestSettings() ) {
				if( (testSettings.Mode & testMode) != testMode) continue;
				testSettings.Mode = testMode;
				var userFixtureType = typeof(StrategyBaseTest);
				var strategyTest = (StrategyBaseTest) Reflect.Construct(userFixtureType, new object[] { "CheckCanBuild", testSettings } );
				foreach( var modelName in strategyTest.GetModelNames()) {
					result = true; // If at least one entry.
					break;
				}
			}
			return result;
		}
	}
}
