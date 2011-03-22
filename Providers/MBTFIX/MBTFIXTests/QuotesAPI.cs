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

#define FOREX
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using NUnit.Framework;
using TickZoom.Api;
using TickZoom.MBTFIX;
using TickZoom.MBTQuotes;

namespace Test
{
	
	[TestFixture]
	public class LoopTest
	{
		private static Log log = Factory.SysLog.GetLogger(typeof(QuotesAPI));
		
		public class TestItem {
			public int Number;
		}
		
		[Test]
		public void ConfigFileTest() {
			string configFileName = @"./Test.config";
			var configFile = new ConfigFile(configFileName);
			var expected = "32";
			configFile.SetValue("Another/Simulate/MarkWalter",expected);
			var value = configFile.GetValue("Another/Simulate/MarkWalter");
			Assert.AreEqual(expected,value);
		}

		[Test]
		public void LinkedListTest() {
			ActiveList<TestItem> linked = new ActiveList<TestItem>();
			List<TestItem> list = new List<TestItem>();
			int size = 1000;
			int iterations = 100000;
			for( int i= 0; i< size; i++) {
				linked.AddLast(new TestItem());
				list.Add(new TestItem());
			}
			long startTime = Factory.TickCount;
			for( int j=0; j< iterations; j++) {
				for( int i= 0; i<list.Count; i++) {
					TestItem item = list[i];
					item.Number += 2;
				}
			}
			long endTime = Factory.TickCount;
			log.Notice("for list took " + (endTime - startTime));
			
			startTime = Factory.TickCount;
			for( int j=0; j< iterations; j++) {
				foreach( var item in list) {
					item.Number += 2;
				}
			}
			endTime = Factory.TickCount;
			log.Notice("foreach list took " + (endTime - startTime));

			startTime = Factory.TickCount;
			for( int j=0; j< iterations; j++) {
				foreach( var item in linked) {
					item.Number += 2;
				}
			}
			endTime = Factory.TickCount;
			log.Notice("foreach linked took " + (endTime - startTime));
			
			startTime = Factory.TickCount;
			for( int j=0; j< iterations; j++) {
				for( var node = linked.First; node != null; node = node.Next) {
					node.Value.Number += 2;
				}
			}
			endTime = Factory.TickCount;
			log.Notice("for on linked took " + (endTime - startTime));
			
			startTime = Factory.TickCount;
			for( int j=0; j< iterations; j++) {
				var next = linked.First;
				for( var node = next; node != null; node = next) {
					next = node.Next;
				    node.Value.Number += 2;
				}
			}
			endTime = Factory.TickCount;
			log.Notice("lambda on linked took " + (endTime - startTime));
		}
	}
	
	[TestFixture]
	public class QuotesAPI 
	{
		private static Log log = Factory.SysLog.GetLogger(typeof(QuotesAPI));

		public QuotesAPI()
		{
		}
		
		[Test]
		public void LinkedListTest() {
			LinkedList<int> linked = new LinkedList<int>();
			List<int> list = new List<int>();
			for( int i= 0; i< 1000000; i++) {
				linked.AddLast(i);
				list.Add(i);
			}
			long startTime = Factory.TickCount;
			for( int i= 0; i<list.Count; i++) {
				list[i] += 1;
			}
			long endTime = Factory.TickCount;
			log.Notice("for list took " + (endTime - startTime));
			
		}

		[Test]
		public void StockQuotePOSTTest() {
//			PostSubmitter post=new PostSubmitter();
//			post.Url="https://www.mbtrading.com/secure/getquoteserverxml.asp";
//			post.PostItems.Add("username","DEMOXJRX");
//			post.PostItems.Add("password","1clock2bird");
//			string message=post.Post();
//			string expectedMessage = "";
//			Assert.AreEqual(expectedMessage,message);
		}
		
//		[Test]
//		public void ForexQuotePOSTTest() {
//			Progress progress = new Progress();
//			PostSubmitter post = new PostSubmitter(progress);
//			post.Url="https://www.mbtrading.com/secure/getquoteserverxml.asp";
//			post.PostItems.Add("username","DEMOYZPS");
//			post.PostItems.Add("password","1step2wax");
//			string message=post.Post();
//			string expectedMessage = @"<xml><logins username=""DEMOYZPS"" quote_Server=""";
//			Assert.IsTrue(message.StartsWith(expectedMessage));
//		}
		
		private void OnException( Exception ex) {
			log.Error("Exception occurred", ex);
		}
		
		[Test]
		public void ConnectToQuotes() {
			string addrStr = "216.52.236.111";
			ushort port = 5020;
			string userName = "DEMOYZPS";
			string password = "1step2wax";
//			string userName = "DEMOXJRX";
//			string password = "1clock2bird";
			using( Socket socket = Factory.Provider.Socket("TestSocket")) {
				socket.MessageFactory = new MessageFactoryMbtQuotes();
				socket.SetBlocking(true);
				socket.Connect(addrStr,port);
				socket.SetBlocking(false);
				Factory.Provider.Manager.AddReader(socket);
				Factory.Provider.Manager.AddWriter(socket);
		
				
				Message message = socket.CreateMessage();
				string hashPassword = MBTQuotesProvider.Hash(password);
				string login = "L|100="+userName+";133="+hashPassword+"\n";
				message.DataOut.Write(login.ToCharArray());
				while( !socket.TrySendMessage(message)) {
					Factory.Parallel.Yield();
				}
				while( !socket.TryGetMessage(out message)) {
					Factory.Parallel.Yield();
				}
				string response = new string(message.DataIn.ReadChars(message.Remaining));
				string expectedResponse = "G|100=DEMOYZPS;8055=demo01\n";
				Assert.AreEqual(expectedResponse,response);
			}
		}
	}
}
