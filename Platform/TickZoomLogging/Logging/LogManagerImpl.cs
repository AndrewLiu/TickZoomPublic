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
using System.Xml;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;
using log4net.Repository.Hierarchy;
using TickZoom.Api;

namespace TickZoom.Logging
{
	public class LogManagerImpl : LogManager {
		private static Log exceptionLog;
		private static object locker = new object();
	    private object repositoryLocker = new object();
		private ILoggerRepository repository;
		private string repositoryName;
        private string currentExtension;
        Dictionary<string, LogImpl> map = new Dictionary<string, LogImpl>();
        private Dictionary<LogMessage,string> logFormats = new Dictionary<LogMessage, string>();

	    public Dictionary<LogMessage,string> Formats
	    {
	        get { return logFormats; }
	    }

		public void ConfigureSysLog() {
			this.repositoryName = "SysLog";
            lock( repositoryLocker)
            {
                this.repository = LoggerManager.CreateRepository(repositoryName);
            }
			Reconfigure(null,GetSysLogDefault());
		    var collection = logFormats;
                collection.Add( LogMessage.LOGMSG1,"for assert {0}");
                collection.Add( LogMessage.LOGMSG2,"{0} subdirectories:");
                collection.Add( LogMessage.LOGMSG3,"    {0} {1} bytes");
                collection.Add( LogMessage.LOGMSG4,null);
                collection.Add( LogMessage.LOGMSG5,"bytes");
                collection.Add( LogMessage.LOGMSG6,"{0} {1}: {2}");
                collection.Add( LogMessage.LOGMSG7,"SoftwareExpiration()");
                collection.Add( LogMessage.LOGMSG8,"ExpirationTime()");
                collection.Add( LogMessage.LOGMSG9,"AbsoluteValue()");
                collection.Add( LogMessage.LOGMSG10,"TestExpired()");
                collection.Add( LogMessage.LOGMSG11,"TestRandom()");
                collection.Add( LogMessage.LOGMSG12,"TestCrazyExpiredTime()");
                collection.Add( LogMessage.LOGMSG13,"TestCrazyExpiredProcess()");
                collection.Add( LogMessage.LOGMSG14,"TestFrequency()");
                collection.Add( LogMessage.LOGMSG15,"{0}.new");
                collection.Add( LogMessage.LOGMSG16,"{0}.BeforeInitialize() - NotImplemented");
                collection.Add( LogMessage.LOGMSG17,"{0}.Initialize() - NotImplemented");
                collection.Add( LogMessage.LOGMSG18,"{0}.StartHistorical() - NotImplemented");
                collection.Add( LogMessage.LOGMSG19,"{0}.BeforeIntervalOpen() - NotImplemented");
                collection.Add( LogMessage.LOGMSG20,"{0}.BeforeIntervalOpen({1}) - NotImplemented");
                collection.Add( LogMessage.LOGMSG21,"{0}.IntervalOpen() - NotImplemented");
                collection.Add( LogMessage.LOGMSG22,"{0}.IntervalOpen({1}) - NotImplemented");
                collection.Add( LogMessage.LOGMSG23,"{0}.ProcessTick({1}) - NotImplemented");
                collection.Add( LogMessage.LOGMSG24,"{0}.BeforeIntervalClose() - NotImplemented");
                collection.Add( LogMessage.LOGMSG25,"{0}.BeforeIntervalClose({1}) - NotImplemented");
                collection.Add( LogMessage.LOGMSG26,"{0}.IntervalClose() - NotImplemented");
                collection.Add( LogMessage.LOGMSG27,"{0}.IntervalClose({1}) - NotImplemented");
                collection.Add( LogMessage.LOGMSG28,"{0}.EndHistorical() - NotImplemented");
                collection.Add( LogMessage.LOGMSG29,"Writing ticks to file.");
                collection.Add( LogMessage.LOGMSG30,"Initialized Variables.");
                collection.Add( LogMessage.LOGMSG31,"Total Diff Bytes = {0}");
                collection.Add( LogMessage.LOGMSG32,"Reading ticks back from file.");
                collection.Add( LogMessage.LOGMSG33,"{0} {1}");
                collection.Add( LogMessage.LOGMSG34,"Close BarCount={0}");
                collection.Add( LogMessage.LOGMSG35,"Last Tick={0}");
                collection.Add( LogMessage.LOGMSG36,"Bar {0}");
                collection.Add( LogMessage.LOGMSG37,"{0} {1} O,H,L,C:{2},{3},{4},{5}");
                collection.Add( LogMessage.LOGMSG38,"{0} {1} {2}");
                collection.Add( LogMessage.LOGMSG39,"Beginning wait for events. ");
                collection.Add( LogMessage.LOGMSG40,"{0}: tick.Time={1}");
                collection.Add( LogMessage.LOGMSG41,"Tick {0} = {1}");
                collection.Add( LogMessage.LOGMSG42,"O: {0} H: {1} L: {2} C: {3} Start: {4} End: {5} Ticks: {6}");
                collection.Add( LogMessage.LOGMSG43,"Close BarCount={0}TickCount");
                collection.Add( LogMessage.LOGMSG44,"Starting TickTest Setup");
                collection.Add( LogMessage.LOGMSG45,"Factorial {0} = {1}");
                collection.Add( LogMessage.LOGMSG46,"'{0}' calling model = {1}");
                collection.Add( LogMessage.LOGMSG47,"{0}.CreateInstance()");
                collection.Add( LogMessage.LOGMSG48,"Dispose()");
                collection.Add( LogMessage.LOGMSG49,"Stopping the page store for trade data.");
                collection.Add( LogMessage.LOGMSG50,"new FormulaDriver({0})");
                collection.Add( LogMessage.LOGMSG51,"{0}.EngineVerifyPeriods()");
                collection.Add( LogMessage.LOGMSG52,"{0}.IntervalDefault({1})");
                collection.Add( LogMessage.LOGMSG53,"{0}.SymbolDefault({1})");
                collection.Add( LogMessage.LOGMSG54,"{0}.Chart({1})");
                collection.Add( LogMessage.LOGMSG55,"WaitTillReady: {0} waiting for bar {1} on {2}, message: {3}");
                collection.Add( LogMessage.LOGMSG56,"{0}.EngineInitialize()");
                collection.Add( LogMessage.LOGMSG57,"{0}.EngineIntervalOpen({1})");
                collection.Add( LogMessage.LOGMSG58,"{0}.EngineIntervalClose({1})");
                collection.Add( LogMessage.LOGMSG59,"{0}.EngineSynchronizePortfolio()");
                collection.Add( LogMessage.LOGMSG60,"FormulaDriver.Initialize({0})");
                collection.Add( LogMessage.LOGMSG61,"FormulaDriver.Initialize: found in driverLookup");
                collection.Add( LogMessage.LOGMSG62,"{0}: Order change: {1}");
                collection.Add( LogMessage.LOGMSG63,"{0}: Switched nextbar to active: {1}");
                collection.Add( LogMessage.LOGMSG64,"AutoCancel canceled {0}");
                collection.Add( LogMessage.LOGMSG65,"{0}: Apparent price change event for: {1}");
                collection.Add( LogMessage.LOGMSG66,"{0}: Resulting Sorted Order {1} List:");
                collection.Add( LogMessage.LOGMSG67,"{0}: Arrange by Status: {1}");
                collection.Add( LogMessage.LOGMSG68,"{0}: -- NextBar order was already Active. Changing back to Active status.");
                collection.Add( LogMessage.LOGMSG69,"{0}: -- Assigning to next bar list.");
                collection.Add( LogMessage.LOGMSG70,"{0}: -- Removing from all lists.");
                collection.Add( LogMessage.LOGMSG71,"Reversing simulate order must be split. Part simulated, other part sent to broker.");
                collection.Add( LogMessage.LOGMSG72,"{0}: -- Assigning to simulate list.");
                collection.Add( LogMessage.LOGMSG73,"{0}: -- Assigning to active list.");
                collection.Add( LogMessage.LOGMSG74,"{0}: Adjusting simulated order: {1}");
                collection.Add( LogMessage.LOGMSG75,"{0}: ArrangeAllByStatus() {1} change orders.");
                collection.Add( LogMessage.LOGMSG76,"{0}: Currently active logical orders:\n{1}");
                collection.Add( LogMessage.LOGMSG77,"{0}: Strategy #{1} for {2} was already taken. Ignoring: {3}");
                collection.Add( LogMessage.LOGMSG78,"{0}: Strategy #{1} for {2} was already taken by: {3}");
                collection.Add( LogMessage.LOGMSG79,"{0}: Strategy #{1} for {2}, was accepted.");
                collection.Add( LogMessage.LOGMSG80,"Assigned order to Decrease group: {0}");
                collection.Add( LogMessage.LOGMSG81,"Assigned order to Increase group: {0}");
                collection.Add( LogMessage.LOGMSG82,"Assigned order to Neutral group: {0}");
                collection.Add( LogMessage.LOGMSG83,"OrderUpdate Starting.");
                collection.Add( LogMessage.LOGMSG84,"PositionUpdate Starting.");
                collection.Add( LogMessage.LOGMSG85,"PositionUpdate Complete.");
                collection.Add( LogMessage.LOGMSG86,"PositionUpdate: {0}={1}");
                collection.Add( LogMessage.LOGMSG87,"ExecutionReport Complete.");
                collection.Add( LogMessage.LOGMSG88,"Writing Error Message: {0}");
                collection.Add( LogMessage.LOGMSG89,"Request Session Update: \n{0}");
                collection.Add( LogMessage.LOGMSG90,"Login message: \n{0}");
                collection.Add( LogMessage.LOGMSG91,"Ignoring execution report of sequence {0} because transact time {1} is earlier than last sequence reset {2}");
                collection.Add( LogMessage.LOGMSG92,"Received Test Request");
                collection.Add( LogMessage.LOGMSG93,"Received Heartbeat");
                collection.Add( LogMessage.LOGMSG94,"TryEndRecovery Status {0}, Session Status Online {1}, Resend Complete {2}");
                collection.Add( LogMessage.LOGMSG95,"Found session status for {0} or {1}: {2}");
                collection.Add( LogMessage.LOGMSG96,"Order server connected (new {0}, previous {1}");
                collection.Add( LogMessage.LOGMSG97,"PositionUpdate for {0}: MBT actual ={1}, TZ actual={2}");
                collection.Add( LogMessage.LOGMSG98,"ExecutionReport New: {0}");
                collection.Add( LogMessage.LOGMSG99,"New order message for Forex Stop: {0}");
                collection.Add( LogMessage.LOGMSG100,"ExecutionReport Partial: {0}");
                collection.Add( LogMessage.LOGMSG101,"ExecutionReport Filled: {0}");
                collection.Add( LogMessage.LOGMSG102,"ExecutionReport Replaced: {0}");
                collection.Add( LogMessage.LOGMSG103,"ExecutionReport Canceled: {0}");
                collection.Add( LogMessage.LOGMSG104,"ExecutionReport Pending Cancel: {0}");
                collection.Add( LogMessage.LOGMSG105,"Pending cancel of multifunction order, so removing {0} and {1}");
                collection.Add( LogMessage.LOGMSG106,"ExecutionReport Reject: {0}");
                collection.Add( LogMessage.LOGMSG107,"ExecutionReport Suspended: {0}");
                collection.Add( LogMessage.LOGMSG108,"ExecutionReport Pending New: {0}");
                collection.Add( LogMessage.LOGMSG109,"Ignoring restated message 150=D for Forex stop execution report 39=A.");
                collection.Add( LogMessage.LOGMSG110,"ExecutionReport Pending Replace: {0}");
                collection.Add( LogMessage.LOGMSG111,"ExecutionReport Resumed: {0}");
                collection.Add( LogMessage.LOGMSG112,"TryHandlePiggyBackFill triggering fill because LastQuantity = {0}");
                collection.Add( LogMessage.LOGMSG113,"CancelRejected: {0}");
                collection.Add( LogMessage.LOGMSG114,"SendFill( {0})");
                collection.Add( LogMessage.LOGMSG115,"Sending physical fill: {0}");
                collection.Add( LogMessage.LOGMSG116,"OnCreateBrokerOrder {0}. Connection {1}, IsOrderServerOnline {2}");
                collection.Add( LogMessage.LOGMSG117,"Adding Order to open order list: {0}");
                collection.Add( LogMessage.LOGMSG118,"Setting replace property of {0} to {1}");
                collection.Add( LogMessage.LOGMSG119,"Change order: \n{0}");
                collection.Add( LogMessage.LOGMSG120,"Create new order: \n{0}");
                collection.Add( LogMessage.LOGMSG121,"Resending cancel order: {0}");
                collection.Add( LogMessage.LOGMSG122,"Resending order: {0}");
                collection.Add( LogMessage.LOGMSG123,"OnCancelBrokerOrder {0}. Connection {1}, IsOrderServerOnline {2}");
                collection.Add( LogMessage.LOGMSG124,"OnChangeBrokerOrder( {0}. Connection {1}, IsOrderServerOnline {2}");
                collection.Add( LogMessage.LOGMSG125,"Received heartbeat response.");
                collection.Add( LogMessage.LOGMSG126,"Sending end of order list: {0}");
                collection.Add( LogMessage.LOGMSG127,"Sending end of position list: {0}");
                collection.Add( LogMessage.LOGMSG128,"Simulating create order reject of 35={0}");
                collection.Add( LogMessage.LOGMSG129,"Simulating order server offline business reject of 35={0}");
                collection.Add( LogMessage.LOGMSG130,"FIXChangeOrder() for {0}. Client id: {1}. Original client id: {2}");
                collection.Add( LogMessage.LOGMSG131,"{0}: Rejected {1}. Cannot change order: {2}. Already filled or canceled.  Message: {3}");
                collection.Add( LogMessage.LOGMSG132,"{0}: Cannot cancel order by client id: {1}. Order Server Offline.");
                collection.Add( LogMessage.LOGMSG133,"Simulating cancel order reject of 35={0}");
                collection.Add( LogMessage.LOGMSG134,"FIXCancelOrder() for {0}. Original client id: {1}");
                collection.Add( LogMessage.LOGMSG135,"{0}: Cannot cancel order by client id: {1}. Probably already filled or canceled.");
                collection.Add( LogMessage.LOGMSG136,"FIXCreateOrder() for {0}. Client id: {1}");
                collection.Add( LogMessage.LOGMSG137,"{0}: Rejected {1}. Order server offline.");
                collection.Add( LogMessage.LOGMSG138,"Received physical Order: {0}");
                collection.Add( LogMessage.LOGMSG139,"Sending reject order: {0}");
                collection.Add( LogMessage.LOGMSG140,"Converting physical fill to FIX: {0}");
                collection.Add( LogMessage.LOGMSG141,"Sending reject cancel.{0}");
                collection.Add( LogMessage.LOGMSG142,"Sending execution report: {0}");
                collection.Add( LogMessage.LOGMSG143,"Login response: {0}");
                collection.Add( LogMessage.LOGMSG144,"Local Write: {0}");
                collection.Add( LogMessage.LOGMSG145,"TrySendTick( {0} {1})");
                collection.Add( LogMessage.LOGMSG146,"Tick message: {0}");
                collection.Add( LogMessage.LOGMSG147,"Added tick to packet: {0}");
                collection.Add( LogMessage.LOGMSG148,"Enqueued tick packet: {0}");
                collection.Add( LogMessage.LOGMSG149,"Sending: {0}");
                collection.Add( LogMessage.LOGMSG150,"Invalid quotes login response ignored: {0}");
                collection.Add( LogMessage.LOGMSG151,"Received tick: {0}");
                collection.Add( LogMessage.LOGMSG152,"Ping request: {0}");
                collection.Add( LogMessage.LOGMSG153,"Ping response successfully received.");
                collection.Add( LogMessage.LOGMSG154,"Got last trade price: {0}");
                collection.Add( LogMessage.LOGMSG155,"Symbol request: {0}");
                collection.Add( LogMessage.LOGMSG156,"{0} received {1} ticks.");
                collection.Add( LogMessage.LOGMSG157,"SetReadableBytes({0})");
                collection.Add( LogMessage.LOGMSG158,"ParseData(): {0}");
                collection.Add( LogMessage.LOGMSG159,"Received Execution Report");
                collection.Add( LogMessage.LOGMSG160,"Received Cancel Rejected");
                collection.Add( LogMessage.LOGMSG161,"Received Business Reject");
                collection.Add( LogMessage.LOGMSG162,"Setting Order Server online");
                collection.Add( LogMessage.LOGMSG163,"Resulting orders in snapshot:");
                collection.Add( LogMessage.LOGMSG164,"Setup Fix Factory");
                collection.Add( LogMessage.LOGMSG165,"Set local sequence number to {0}");
                collection.Add( LogMessage.LOGMSG166,"Requesting heartbeat: {0}");
                collection.Add( LogMessage.LOGMSG167,"{0}: Rejected {1}. Order quantity must be greater then cumulative filled quantity: {2}");
                collection.Add( LogMessage.LOGMSG168,"Cannot cancel order by client id: {0}. Probably already filled or canceled.");
                collection.Add( LogMessage.LOGMSG169,"Trade {0} at {1} ");
                collection.Add( LogMessage.LOGMSG170,"Trade {0} at {1} time: {2}");
                collection.Add( LogMessage.LOGMSG171,"Mapped from {0} to {1}");
                collection.Add( LogMessage.LOGMSG172,"Ask {0} at {1} size {2} time: {3}");
                collection.Add( LogMessage.LOGMSG173,"Bid {0} at {1} size {2} time: {3}");
                collection.Add( LogMessage.LOGMSG174,"{0}: Bid {1} Ask: {2} BidShares {3} AskShares: {4}");
                collection.Add( LogMessage.LOGMSG175,"Quote not top of book");
                collection.Add( LogMessage.LOGMSG176,"Message HexDump: {0}");
                collection.Add( LogMessage.LOGMSG177,"quote isTrade={0} isQuote={1}");
                collection.Add( LogMessage.LOGMSG178,"Sending Ask {0}");
                collection.Add( LogMessage.LOGMSG179,"Sending Bid {0}");
                collection.Add( LogMessage.LOGMSG180,"Starting FIX Simulator.");
                collection.Add( LogMessage.LOGMSG181,"ProcessQuotePackets( {0} packets in queue.)");
                collection.Add( LogMessage.LOGMSG182,"Local Read: {0}");
                collection.Add( LogMessage.LOGMSG183,"Sending tick: {0}");
                collection.Add( LogMessage.LOGMSG184,"Created timer. (Default startTime: {0})");
                collection.Add( LogMessage.LOGMSG185,"> Initialize.");
                collection.Add( LogMessage.LOGMSG186,"Socket state now: {0}");
                collection.Add( LogMessage.LOGMSG187,"Created new {0}");
                collection.Add( LogMessage.LOGMSG188,"Invoke() Current socket state: {0}, {1}");
                collection.Add( LogMessage.LOGMSG189,"Socket state changed to: {0}");
                collection.Add( LogMessage.LOGMSG190,"Retrying in {0} seconds.");
                collection.Add( LogMessage.LOGMSG191,"Stopped socket task.");
                collection.Add( LogMessage.LOGMSG192,"Stopped task timer.");
                collection.Add( LogMessage.LOGMSG193,"Connection status changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG194,"Remote Read: {0}");
                collection.Add( LogMessage.LOGMSG195,"Remote Write: {0}");
                collection.Add( LogMessage.LOGMSG196,"Start() Agent: {0}");
                collection.Add( LogMessage.LOGMSG197,"> SetupFolders.");
                collection.Add( LogMessage.LOGMSG198,"More than 3 heart beats sent after frozen.  Ending heartbeats.");
                collection.Add( LogMessage.LOGMSG199,"SocketState changed to {0}");
                collection.Add( LogMessage.LOGMSG200,"SyntheticReject: {0}");
                collection.Add( LogMessage.LOGMSG201,"SyntheticFill: {0}");
                collection.Add( LogMessage.LOGMSG202,"Found sequence {0} on the resend queue. Requesting resend from {1} to {2}");
                collection.Add( LogMessage.LOGMSG203,"Received FIX Message: {0}");
                collection.Add( LogMessage.LOGMSG204,"FIX Server login message sequence was lower than expected. Resetting to {0}");
                collection.Add( LogMessage.LOGMSG205,"Logout message received.");
                collection.Add( LogMessage.LOGMSG206,"Received gap fill. Setting next sequence = {0}");
                collection.Add( LogMessage.LOGMSG207,"Found Sending Time Accuracy message request for message: {0}");
                collection.Add( LogMessage.LOGMSG208,"Sending Time Accuracy Problem -- Resending Message.");
                collection.Add( LogMessage.LOGMSG209,"Starting history dump");
                collection.Add( LogMessage.LOGMSG210,"Fiinished history dump");
                collection.Add( LogMessage.LOGMSG211,"Login sequence matched. Incrementing remote sequence to {0}");
                collection.Add( LogMessage.LOGMSG212,"Login remote sequence {0} mismatch expected sequence {1}. Resend needed.");
                collection.Add( LogMessage.LOGMSG213,"Already received sequence {0}. Expecting {1} as next sequence. Ignoring. \n{2}");
                collection.Add( LogMessage.LOGMSG214,"Incrementing remote sequence to {0}");
                collection.Add( LogMessage.LOGMSG215,"Sequence is {0} but expected sequence is {1}. Buffering message.");
                collection.Add( LogMessage.LOGMSG216,"Expected resend sequence set to {0}");
                collection.Add( LogMessage.LOGMSG217," Sending resend request: {0}");
                collection.Add( LogMessage.LOGMSG218,"Sending gap fill message {0} to {1}");
                collection.Add( LogMessage.LOGMSG219,"Found resend request for {0} to {1}: {2}");
                collection.Add( LogMessage.LOGMSG220,"Send FIX message: \n{0}");
                collection.Add( LogMessage.LOGMSG221,"LogOut() status {0}");
                collection.Add( LogMessage.LOGMSG222,"Calling OnLogOut()");
                collection.Add( LogMessage.LOGMSG223,"Resend Complete changed to {0}");
                collection.Add( LogMessage.LOGMSG224,"Sending {0} for {1} ...");
                collection.Add( LogMessage.LOGMSG225,"Attempted RequestPosition but IsRecovered is {0}");
                collection.Add( LogMessage.LOGMSG226,"Attempted RequestPosition but isBrokerStarted is {0}");
                collection.Add( LogMessage.LOGMSG227,"Attempted StartBroker but IsRecovered is {0}");
                collection.Add( LogMessage.LOGMSG228,"Attempted StartBroker but isBrokerStarted is already {0}");
                collection.Add( LogMessage.LOGMSG229,"Sending StartBroker for {0}. Reason: {1}");
                collection.Add( LogMessage.LOGMSG230,"Attempted StartBroker but OrderAlgorithm not yet synchronized");
                collection.Add( LogMessage.LOGMSG231,"Tried to send EndBroker for {0} but broker status is already offline.");
                collection.Add( LogMessage.LOGMSG232,"Sent EndBroker for {0}.");
                collection.Add( LogMessage.LOGMSG233,"PositionChange {0}");
                collection.Add( LogMessage.LOGMSG234,"LimeProvider.OnLogOut() already disposed.");
                collection.Add( LogMessage.LOGMSG235,"Sending RemoteShutdown confirmation back to provider manager.");
                collection.Add( LogMessage.LOGMSG236,"Broker offline but sending fill anyway for {0} to receiver: {1}");
                collection.Add( LogMessage.LOGMSG237,"Sending fill event for {0} to receiver: {1}");
                collection.Add( LogMessage.LOGMSG238,"Broker offline so not sending logical touch for {0}: {1}");
                collection.Add( LogMessage.LOGMSG239,"Sending logical touch for {0} to receiver for logical touch: {1}");
                collection.Add( LogMessage.LOGMSG240,"Order not found for {0}. Probably allready filled or canceled.");
                collection.Add( LogMessage.LOGMSG241,"SendHeartBeat Status {0}, Session Status Online {1}, Resend Complete {2}");
                collection.Add( LogMessage.LOGMSG242,"LimeFIXProvider.Login()");
                collection.Add( LogMessage.LOGMSG243,"Recovered from snapshot Local Sequence {0}, Remote Sequence {1}");
                collection.Add( LogMessage.LOGMSG244,"Recovered orders from snapshot:");
                collection.Add( LogMessage.LOGMSG245,"Recovered symbol positions from snapshot:\n{0}");
                collection.Add( LogMessage.LOGMSG246,"Recovered strategy positions from snapshot:\n{0}");
                collection.Add( LogMessage.LOGMSG247,"Unable to recover from snapshot. Beginning full recovery.");
                collection.Add( LogMessage.LOGMSG248,"StartPositionSync()");
                collection.Add( LogMessage.LOGMSG249,"Found 0 open orders prior to sync.");
                collection.Add( LogMessage.LOGMSG250,"Orders prior to sync:");
                collection.Add( LogMessage.LOGMSG251,"ConnectionStatus changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG252,"Heartbeat occurred at {0}");
                collection.Add( LogMessage.LOGMSG253,"Simulating FIX connection was lost, closing FIX socket.");
                collection.Add( LogMessage.LOGMSG254,"Skipping heartbeat because fix state: {0}");
                collection.Add( LogMessage.LOGMSG255,"Heartbeat response was never received.");
                collection.Add( LogMessage.LOGMSG256,"Resetting sequence numbers because simulation rollover.");
                collection.Add( LogMessage.LOGMSG257,"ProcessFIXPackets( {0} packets in queue.)");
                collection.Add( LogMessage.LOGMSG258,"Sending Gap Fill message {0}: \n{1}");
                collection.Add( LogMessage.LOGMSG259,"Sending order server status: {0}");
                collection.Add( LogMessage.LOGMSG260,"Sending resend request: {0}");
                collection.Add( LogMessage.LOGMSG261,"Received FIX message: {0}");
                collection.Add( LogMessage.LOGMSG262,"Login packet sequence {0} was greater than expected {1}");
                collection.Add( LogMessage.LOGMSG263,"Login packet sequence {0} was less than or equal to expected {1} so updating remote sequence...");
                collection.Add( LogMessage.LOGMSG264,"packet sequence {0} greater than expected {1}");
                collection.Add( LogMessage.LOGMSG265,"Already received packet sequence {0}. Ignoring.");
                collection.Add( LogMessage.LOGMSG266,"Resend request with sequence {0}. So updating remote sequence...");
                collection.Add( LogMessage.LOGMSG267,"Sending system offline simulation: {0}");
                collection.Add( LogMessage.LOGMSG268,"Sent login reject {0}");
                collection.Add( LogMessage.LOGMSG269,"SendServerSyncComplete()");
                collection.Add( LogMessage.LOGMSG270,"Sending session status online.");
                collection.Add( LogMessage.LOGMSG271,"Sending login response: {0}");
                collection.Add( LogMessage.LOGMSG272,"Found reset seq number flag. Resetting seq number to {0}");
                collection.Add( LogMessage.LOGMSG273,"Connection Ignoring message: {0}");
                collection.Add( LogMessage.LOGMSG274,"Ignoring message: {0}");
                collection.Add( LogMessage.LOGMSG275,"Ignoring fix message sequence {0}");
                collection.Add( LogMessage.LOGMSG276,"Skipping message: {0}");
                collection.Add( LogMessage.LOGMSG277,"Processing message with {0}. So updating remote sequence...");
                collection.Add( LogMessage.LOGMSG278,"Simulating order 'black hole' of 35={0} by incrementing sequence to {1} but ignoring message with sequence {2}");
                collection.Add( LogMessage.LOGMSG279,"Resending simulated FIX Message: {0}");
                collection.Add( LogMessage.LOGMSG280,"Setting next heartbeat for {0}. State: {1}");
                collection.Add( LogMessage.LOGMSG281,"Skipping send of sequence # {0} to simulate lost message. {1}");
                collection.Add( LogMessage.LOGMSG282,"Message type is: {0}");
                collection.Add( LogMessage.LOGMSG283,"Simulating FIX Message: {0}");
                collection.Add( LogMessage.LOGMSG284,"Remote sequence changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG285,"Sending business reject order: {0}");
                collection.Add( LogMessage.LOGMSG286,"Sending logout confirmation: {0}");
                collection.Add( LogMessage.LOGMSG287,"{0} matched include mask {1}");
                collection.Add( LogMessage.LOGMSG288,"Excluding {0} because of mask {1}");
                collection.Add( LogMessage.LOGMSG289,"Processing Keys: {0}");
                collection.Add( LogMessage.LOGMSG290,"HandleKey({0})");
                collection.Add( LogMessage.LOGMSG291,"Copying buffer at {0}");
                collection.Add( LogMessage.LOGMSG292,"Reading message: \n{0}");
                collection.Add( LogMessage.LOGMSG293,"Copied buffer: {0}");
                collection.Add( LogMessage.LOGMSG294,"int = {0}");
                collection.Add( LogMessage.LOGMSG295,"double = {0}");
                collection.Add( LogMessage.LOGMSG296,"string = {0}");
                collection.Add( LogMessage.LOGMSG297,"skipping {0} bytes.");
                collection.Add( LogMessage.LOGMSG298,"Sending time accuracy problem: {0}  Ignoring by using current time instead.");
                collection.Add( LogMessage.LOGMSG299,"Transaction time accuracy problem: {0}  Ignoring by using current time instead.");
                collection.Add( LogMessage.LOGMSG300,"Regenerate.");
                collection.Add( LogMessage.LOGMSG301,"Wait for graceful socket shutdown because socket state: {0}");
                collection.Add( LogMessage.LOGMSG302,"Requested Connect for {0}");
                collection.Add( LogMessage.LOGMSG303,"Flushing all fill queues.");
                collection.Add( LogMessage.LOGMSG304,"Current FIX Simulator orders.");
                collection.Add( LogMessage.LOGMSG305,"Starting Provider Simulator Support.");
                collection.Add( LogMessage.LOGMSG306,"ShutdownHandlers()");
                collection.Add( LogMessage.LOGMSG307,"There are {0} symbolHandlers.");
                collection.Add( LogMessage.LOGMSG308,"Disposing symbol handler {0}");
                collection.Add( LogMessage.LOGMSG309,"symbolHandlers is null.");
                collection.Add( LogMessage.LOGMSG310,"Dequeue tick {0}.{1}");
                collection.Add( LogMessage.LOGMSG311,"Set next timer for {0}.{1} at {2}.{3}");
                collection.Add( LogMessage.LOGMSG312,"Current time {0} was less than tick time {1}.{2}");
                collection.Add( LogMessage.LOGMSG313,"Sending tick from timer event: {0}");
                collection.Add( LogMessage.LOGMSG314,"Setting fillSimulator.IsOnline false");
                collection.Add( LogMessage.LOGMSG315,"fillSimulator is null.");
                collection.Add( LogMessage.LOGMSG316,"isDisposed {0}");
                collection.Add( LogMessage.LOGMSG317,"Openning tick file for reading.");
                collection.Add( LogMessage.LOGMSG318,"Opening tick file for reading.");
                collection.Add( LogMessage.LOGMSG319,"Locked tickSync for {0}");
                collection.Add( LogMessage.LOGMSG320,"TickSyncChangedEvent({0}) resuming task.");
                collection.Add( LogMessage.LOGMSG321,"TickSyncChangedEvent({0}) not ready to resume task.");
                collection.Add( LogMessage.LOGMSG322,"TryCompleteTick() Next Tick");
                collection.Add( LogMessage.LOGMSG323,"Process physical orders - {0}");
                collection.Add( LogMessage.LOGMSG324,"Reprocess physical orders - {0}");
                collection.Add( LogMessage.LOGMSG325,"End Of Tick Data.");
                collection.Add( LogMessage.LOGMSG326,"Set {0} sequence for = {1}");
                collection.Add( LogMessage.LOGMSG327,"Sequence {0} >= {1} sequence {2} so causing negative test. {3} attempts {4}, count {5}");
                collection.Add( LogMessage.LOGMSG328,"{0}Repeating {1} negative test. Repeat count {2}");
                collection.Add( LogMessage.LOGMSG329,"{0}Random {1} occurred so causing negative test. {2} attempts {3}, count {4}");
                collection.Add( LogMessage.LOGMSG330,"No button found for {0}.{1}()");
                collection.Add( LogMessage.LOGMSG331,"{0}() was not found on class {1}");
                collection.Add( LogMessage.LOGMSG332,"{0}.{1} => {2}.{3}");
                collection.Add( LogMessage.LOGMSG333,"Unabled to find {0}.{1}() to assign to {2}.{3}");
                collection.Add( LogMessage.LOGMSG334,"{0}.{1}() => {2}.{3}");
                collection.Add( LogMessage.LOGMSG335,"No control found for {0}.{1}");
                collection.Add( LogMessage.LOGMSG336,"{0}.{1} was already bound.");
                collection.Add( LogMessage.LOGMSG337,"DataSource => enum values of {0}.{1}");
                collection.Add( LogMessage.LOGMSG338,"Values property not found for {0}.{1}");
                collection.Add( LogMessage.LOGMSG339,"DataSource => {0}.{1}");
                collection.Add( LogMessage.LOGMSG340,"DataSource was not set on control for {0}.{1}");
                collection.Add( LogMessage.LOGMSG341,"Ignoring event from Reader: {0}");
                collection.Add( LogMessage.LOGMSG342,"Writing buffer size {0}");
                collection.Add( LogMessage.LOGMSG343,"{0}\nPausing {1} seconds before retry.");
                collection.Add( LogMessage.LOGMSG344,"Start called.");
                collection.Add( LogMessage.LOGMSG345,"Stop({0})");
                collection.Add( LogMessage.LOGMSG346,"Finished reading to file length: {0}");
                collection.Add( LogMessage.LOGMSG347,"Exception on progressCallback: {0}");
                collection.Add( LogMessage.LOGMSG348,"Ending data read because count reached {0} ticks.");
                collection.Add( LogMessage.LOGMSG349,"Read a tick {0}");
                collection.Add( LogMessage.LOGMSG350,"Allocated box id in reader {0}, count {1}");
                collection.Add( LogMessage.LOGMSG351,"EndHistorical for {0}");
                collection.Add( LogMessage.LOGMSG352,"calling Agent.OnEvent(symbol,EventType.EndHistorical)");
                collection.Add( LogMessage.LOGMSG353,"Signature: {0}");
                collection.Add( LogMessage.LOGMSG354,"{0} int combinations");
                collection.Add( LogMessage.LOGMSG355," 255 int combinations account for {0} out of {1}");
                collection.Add( LogMessage.LOGMSG356,"diff bits = {0}");
                collection.Add( LogMessage.LOGMSG357,"File Name = {0}");
                collection.Add( LogMessage.LOGMSG358,"OpenFileForWriting()");
                collection.Add( LogMessage.LOGMSG359,"Writing to file buffer: {0}");
                collection.Add( LogMessage.LOGMSG360,"Starting to read data.");
                collection.Add( LogMessage.LOGMSG361,"{0} blocks in queue.");
                collection.Add( LogMessage.LOGMSG362,"Exiting Close()");
                collection.Add( LogMessage.LOGMSG363,"CloseFileForReading()");
                collection.Add( LogMessage.LOGMSG364,"CloseFileForWriting() at with length {0}");
                collection.Add( LogMessage.LOGMSG365,"Before flush memory {0}");
                collection.Add( LogMessage.LOGMSG366,"After flush memory {0}");
                collection.Add( LogMessage.LOGMSG367,"Writing buffer size: {0}");
                collection.Add( LogMessage.LOGMSG368,"{0}: {1}\nPausing {2} seconds before retry.");
                collection.Add( LogMessage.LOGMSG369,"Closing dataIn.");
                collection.Add( LogMessage.LOGMSG370,"CloseFile() at with length {0}");
                collection.Add( LogMessage.LOGMSG371,"Before Cx {0}");
                collection.Add( LogMessage.LOGMSG372,"Writing decimal places used in price compression.");
                collection.Add( LogMessage.LOGMSG373,"Writing Reset token during tick compression.");
                collection.Add( LogMessage.LOGMSG374,"Reset Dx {0}");
                collection.Add( LogMessage.LOGMSG375,"Cx tick: {0}");
                collection.Add( LogMessage.LOGMSG376,"Writing decimal places use in price compression.");
                collection.Add( LogMessage.LOGMSG377,"{0} {1}, CheckSum {2}");
                collection.Add( LogMessage.LOGMSG378,"Before Dx {0}");
                collection.Add( LogMessage.LOGMSG379,"Processing decimal place precision during tick de-compression.");
                collection.Add( LogMessage.LOGMSG380,"Processing Reset during tick de-compression.");
                collection.Add( LogMessage.LOGMSG381,"Dx tick: {0}");
                collection.Add( LogMessage.LOGMSG382,"Exiting, queue terminated.");
                collection.Add( LogMessage.LOGMSG383,"Before flush write queue {0}");
                collection.Add( LogMessage.LOGMSG384,"After flush write queue {0}");
                collection.Add( LogMessage.LOGMSG385,"Sending event {0} to tickwriter queue.");
                collection.Add( LogMessage.LOGMSG386,"Finish()");
                collection.Add( LogMessage.LOGMSG387,"Only {0} writes before closeFile but {1} appends.");
                collection.Add( LogMessage.LOGMSG388,"Binary Max Security ID = {0}");
                collection.Add( LogMessage.LOGMSG389,"Security ID = {0}");
                collection.Add( LogMessage.LOGMSG390,"Digit {0} = {1}");
                collection.Add( LogMessage.LOGMSG391,"Security Id = {0}");
                collection.Add( LogMessage.LOGMSG392,"Country Code Char 2 {0} = {1}");
                collection.Add( LogMessage.LOGMSG393,"Country Code Char 1 {0} = {1}");
                collection.Add( LogMessage.LOGMSG394,"SecurityIdToISIN");
                collection.Add( LogMessage.LOGMSG395,"{0}: {1} Direction: {2}");
                collection.Add( LogMessage.LOGMSG396,"Starting Chart Thread");
                collection.Add( LogMessage.LOGMSG397,"Returning Chart Created by Thread");
                collection.Add( LogMessage.LOGMSG398,"Chart Thread Started");
                collection.Add( LogMessage.LOGMSG399,"Handle category {0}");
                collection.Add( LogMessage.LOGMSG400,"Property {0} = {1}");
                collection.Add( LogMessage.LOGMSG401,"Handle {0}");
                collection.Add( LogMessage.LOGMSG402,"Fitness Assigned: {0}");
                collection.Add( LogMessage.LOGMSG403,"SetupSymbolData took {0} seconds and {1} milliseconds");
                collection.Add( LogMessage.LOGMSG404,"Startup took {0} seconds and {1} milliseconds");
                collection.Add( LogMessage.LOGMSG405,"Saves processing on {0}!");
                collection.Add( LogMessage.LOGMSG406,"Before: {0} - {1}");
                collection.Add( LogMessage.LOGMSG407,"After: {0} - {1}");
                collection.Add( LogMessage.LOGMSG408,"Handle Starter properties");
                collection.Add( LogMessage.LOGMSG409,"Handle {0} {1}");
                collection.Add( LogMessage.LOGMSG410,"Method {0}({1})");
                collection.Add( LogMessage.LOGMSG411,"SetSymbols uses the global library of symbols.");
                collection.Add( LogMessage.LOGMSG412,"UpdatePrice({0})");
                collection.Add( LogMessage.LOGMSG413,"direction = {0}, currentPosition = {1}, volume = {2}, closed points = {3}");
                collection.Add( LogMessage.LOGMSG414,"{0}: OnProcessFill: {1}");
                collection.Add( LogMessage.LOGMSG415,"Ignoring fill since it's a simulated fill meaning that the strategy already exited via a money management exit like stop loss or target profit, etc.");
                collection.Add( LogMessage.LOGMSG416,"For portfolio, converted to fill: {0}");
                collection.Add( LogMessage.LOGMSG417,"{0},{1},{2}");
                collection.Add( LogMessage.LOGMSG418,"Changing strategy result position to {0}");
                collection.Add( LogMessage.LOGMSG419,"Enter Trade: {0}");
                collection.Add( LogMessage.LOGMSG420,"Change Trade: {0}");
                collection.Add( LogMessage.LOGMSG421,"Exit Trade: {0}");
                collection.Add( LogMessage.LOGMSG422,"{0},{1},{2},{3}");
                collection.Add( LogMessage.LOGMSG423,"Starting recency {0}");
                collection.Add( LogMessage.LOGMSG424,"PositionChange recency {0} less than {1} so ignoring.");
                collection.Add( LogMessage.LOGMSG425,"PositionChange({0})");
                collection.Add( LogMessage.LOGMSG426,"PositionChange event received while FIX was offline or recovering. Skipping SyncPosition and ProcessOrders.");
                collection.Add( LogMessage.LOGMSG427,"Cannot match a suspended order: {0}");
                collection.Add( LogMessage.LOGMSG428,"Cannot match a filled order: {0}");
                collection.Add( LogMessage.LOGMSG429,"Ignoring cancel broker order {0} as physical order cache has a cancel or replace already.");
                collection.Add( LogMessage.LOGMSG430,"Ignoring broker order while waiting on reject recovery.");
                collection.Add( LogMessage.LOGMSG431,"Cancel Broker Order: {0}");
                collection.Add( LogMessage.LOGMSG432,"Ignoring broker order {0} as physical order cache has a cancel or replace already.");
                collection.Add( LogMessage.LOGMSG433,"Change Broker Order: {0}");
                collection.Add( LogMessage.LOGMSG434,"Create Broker Order {0}");
                collection.Add( LogMessage.LOGMSG435,"Ignoring broker order as physical order cache has a create order already.");
                collection.Add( LogMessage.LOGMSG436,"ProcessMatchPhysicalEntry()");
                collection.Add( LogMessage.LOGMSG437,"position difference = {0}");
                collection.Add( LogMessage.LOGMSG438,"Strategy position is long {0} so canceling {1} order..");
                collection.Add( LogMessage.LOGMSG439,"Strategy position is short {0} so canceling {1} order..");
                collection.Add( LogMessage.LOGMSG440,"PhysicalChange({0}) delta={1}, strategyPosition={2}, difference={3}");
                collection.Add( LogMessage.LOGMSG441,"(Delta=0) Canceling: {0}");
                collection.Add( LogMessage.LOGMSG442,"(Delta) Changing {0} to {1}");
                collection.Add( LogMessage.LOGMSG443,"Delta same as size: Check Price and Side.");
                collection.Add( LogMessage.LOGMSG444,"(Size) Changing {0} to {1}");
                collection.Add( LogMessage.LOGMSG445,"(Side) Canceling {0}");
                collection.Add( LogMessage.LOGMSG446,"(Price) Changing {0} to {1}");
                collection.Add( LogMessage.LOGMSG447,"(Price) Canceling wrong side{0}");
                collection.Add( LogMessage.LOGMSG448,"Process Match()");
                collection.Add( LogMessage.LOGMSG449,"ProcessMissingPhysicalEntry({0})");
                collection.Add( LogMessage.LOGMSG450,"ProcessMissingChange({0})");
                collection.Add( LogMessage.LOGMSG451,"ProcessMissingReverse({0})");
                collection.Add( LogMessage.LOGMSG452,"ProcessMissingExit( strategy position {0}, {1})");
                collection.Add( LogMessage.LOGMSG453,"Skipping position sync because ReceivedDesiredPosition = {0}");
                collection.Add( LogMessage.LOGMSG454,"Skipping position sync because DisableRealtimeSimulation = {0}");
                collection.Add( LogMessage.LOGMSG455,"TrySyncPosition - {0}");
                collection.Add( LogMessage.LOGMSG456,"SyncPosition() found position currently synced. With expected {0} and actual {1} plus pending adjustments {2}");
                collection.Add( LogMessage.LOGMSG457,"SetLogicalOrders() order count = {0}");
                collection.Add( LogMessage.LOGMSG458,"SetLogicalOrders( logicals {0})");
                collection.Add( LogMessage.LOGMSG459,"Market order: {0}");
                collection.Add( LogMessage.LOGMSG460,"Checking for orders pending since: {0}");
                collection.Add( LogMessage.LOGMSG461,"Pending order: {0}");
                collection.Add( LogMessage.LOGMSG462,"Removing pending and stale Cancel order: {0}");
                collection.Add( LogMessage.LOGMSG463,"Cancel failed to send for order: {0}");
                collection.Add( LogMessage.LOGMSG464,"SyntheticFill() physical: {0}");
                collection.Add( LogMessage.LOGMSG465,"SyntheticFill: Cannot find physical order for id {0}");
                collection.Add( LogMessage.LOGMSG466,"LogicalOrder serial number {0} wasn't found for synthetic fill. Must have been canceled. Ignoring.");
                collection.Add( LogMessage.LOGMSG467,"Performing compare to attempt to create the market order for touched order.");
                collection.Add( LogMessage.LOGMSG468,"ProcessFill() physical: {0}");
                collection.Add( LogMessage.LOGMSG469,"Updating actual position from {0} to {1} from fill size {2}");
                collection.Add( LogMessage.LOGMSG470,"Physical order partially filled: {0}");
                collection.Add( LogMessage.LOGMSG471,"Leaving symbol position at desired {0}, since this appears to be an adjustment market order: {1}");
                collection.Add( LogMessage.LOGMSG472,"Skipping logical fill for an adjustment market order.");
                collection.Add( LogMessage.LOGMSG473,"Performing extra compare.");
                collection.Add( LogMessage.LOGMSG474,"Logical order not found. So logical was already canceled: {0}");
                collection.Add( LogMessage.LOGMSG475,"Already canceled because physical order price {0} differs from logical order price {1}");
                collection.Add( LogMessage.LOGMSG476,"OffsetTooLateToChange {0}");
                collection.Add( LogMessage.LOGMSG477,"isFilledAfterCancel {0}");
                collection.Add( LogMessage.LOGMSG478,"OffsetTooLateToCancel {0}");
                collection.Add( LogMessage.LOGMSG479,"Will sync positions because fill from order already canceled: {0}");
                collection.Add( LogMessage.LOGMSG480,"Adjusting symbol position to desired {0}, physical fill was {1}");
                collection.Add( LogMessage.LOGMSG481,"Creating logical fill with position {0} from strategy position {1}");
                collection.Add( LogMessage.LOGMSG482,"strategy position {0} differs from logical order position {1} for {2}");
                collection.Add( LogMessage.LOGMSG483,"Fill price: {0}");
                collection.Add( LogMessage.LOGMSG484,"Not sending logical touch for: {0}");
                collection.Add( LogMessage.LOGMSG485,"Physical order completely filled: {0}");
                collection.Add( LogMessage.LOGMSG486,"Found this order in the replace property. Removing it also: {0}");
                collection.Add( LogMessage.LOGMSG487,"PerformCompareInternal() returned: {0}");
                collection.Add( LogMessage.LOGMSG488,"PerformCompare finished - {0}");
                collection.Add( LogMessage.LOGMSG489,"PerformCompare ignored. Position not yet synced. {0}");
                collection.Add( LogMessage.LOGMSG490,"Skipping ProcesOrders. RecursiveCounter {0} tick {1}");
                collection.Add( LogMessage.LOGMSG491,"ConfirmedOrderCount {0} greater than zero so resetting reject counter.");
                collection.Add( LogMessage.LOGMSG492,"ProcessFill() logical: {0}");
                collection.Add( LogMessage.LOGMSG493,"Matched fill with order: {0}");
                collection.Add( LogMessage.LOGMSG494,"Change order fill = {0}, strategy = {1}, fill = {2}");
                collection.Add( LogMessage.LOGMSG495,"Changing order to position: {0}");
                collection.Add( LogMessage.LOGMSG496,"LogicalOrder is completely filled.");
                collection.Add( LogMessage.LOGMSG497,"Found a entry order which flattened the position. Likely due to bracketed entries that both get filled: {0}");
                collection.Add( LogMessage.LOGMSG498,"Found complete physical fill but incomplete logical fill. Physical orders...");
                collection.Add( LogMessage.LOGMSG499,"Sending logical fill for {0}: {1}");
                collection.Add( LogMessage.LOGMSG500,"Marking order id {0} as completely filled.");
                collection.Add( LogMessage.LOGMSG501,"Canceling via OCO {0}");
                collection.Add( LogMessage.LOGMSG502,"Canceling all change orders since strategy position {0}");
                collection.Add( LogMessage.LOGMSG503,"Canceling all entry orders after an entry order was filled.");
                collection.Add( LogMessage.LOGMSG504,"Canceling all exits, exit strategies, entries, and change orders after an exit or exit strategy was filled.");
                collection.Add( LogMessage.LOGMSG505,"Canceling all reverse and entry orders after a reverse order was filled.");
                collection.Add( LogMessage.LOGMSG506,"Canceling all entry and change orders after a partial exit, exit strategy, or reverse order.");
                collection.Add( LogMessage.LOGMSG507,"Adjusting strategy position from {0} to {1}. Recency {2} for strategy id {3}");
                collection.Add( LogMessage.LOGMSG508,"ProcessOrders()");
                collection.Add( LogMessage.LOGMSG509,"PerformCompare for {0} with {1} actual {2} desired. Positions {3}.");
                collection.Add( LogMessage.LOGMSG510,"{0} logicals, {1} physicals.");
                collection.Add( LogMessage.LOGMSG511,"Found pending physical orders. So ending order comparison.");
                collection.Add( LogMessage.LOGMSG512,"Found pending physical orders. So only checking for extra physicals.");
                collection.Add( LogMessage.LOGMSG513,"logical order didn't match: {0}");
                collection.Add( LogMessage.LOGMSG514,"Found {0} extra physicals.");
                collection.Add( LogMessage.LOGMSG515,"Extra physical orders: {0}");
                collection.Add( LogMessage.LOGMSG516,"Found {0} extra logicals.");
                collection.Add( LogMessage.LOGMSG517,"Extra logical order: {0}");
                collection.Add( LogMessage.LOGMSG518,"Buffered logicals were updated so refreshing original logicals list ...");
                collection.Add( LogMessage.LOGMSG519,"Logical Order: {0}");
                collection.Add( LogMessage.LOGMSG520,"Listing {0} orders:");
                collection.Add( LogMessage.LOGMSG521,"Empty list of {0} orders.");
                collection.Add( LogMessage.LOGMSG522,"Changed actual postion to {0}");
                collection.Add( LogMessage.LOGMSG523,"ConfirmChange({0}) {1}");
                collection.Add( LogMessage.LOGMSG524,"ConfirmChange: Cannot find physical order for id {0}");
                collection.Add( LogMessage.LOGMSG525,"Changed {0}");
                collection.Add( LogMessage.LOGMSG526,"ConfirmActive({0}) {1}");
                collection.Add( LogMessage.LOGMSG527,"ConfirmCreate({0}) {1}");
                collection.Add( LogMessage.LOGMSG528,"RejectOrder: Cannot find physical order for id {0}. Probably already filled or canceled.");
                collection.Add( LogMessage.LOGMSG529,"RejectOrder({0}, {1}) {2}");
                collection.Add( LogMessage.LOGMSG530,"Removing expired order: {0}");
                collection.Add( LogMessage.LOGMSG531,"ConfirmCancel: Cannot find physical order for id {0}");
                collection.Add( LogMessage.LOGMSG532,"ConfirmCancel({0}) {1}");
                collection.Add( LogMessage.LOGMSG533,"GetActiveOrders( {0})");
                collection.Add( LogMessage.LOGMSG534,"Including order: {0}");
                collection.Add( LogMessage.LOGMSG535,"Excluding order: {0}");
                collection.Add( LogMessage.LOGMSG536,"Received strategy position. {0}");
                collection.Add( LogMessage.LOGMSG537,"SetActualPosition( {0} = {1})");
                collection.Add( LogMessage.LOGMSG538,"PurgeOriginalOrder( {0})");
                collection.Add( LogMessage.LOGMSG539,"RemoveOrder( {0})");
                collection.Add( LogMessage.LOGMSG540,"Removed order by broker id {0}: {1}");
                collection.Add( LogMessage.LOGMSG541,"Removed order by logical serial {0}: {1}");
                collection.Add( LogMessage.LOGMSG542,"Create ignored because order was already on create order queue: {0}");
                collection.Add( LogMessage.LOGMSG543,"Cancel or Changed ignored because previous order order working for: {0}");
                collection.Add( LogMessage.LOGMSG544,"Assigning order {0} with {1}");
                collection.Add( LogMessage.LOGMSG545,"Resetting last change time for all physical orders.");
                collection.Add( LogMessage.LOGMSG546,"Cancel or Changed ignored because pervious order order working for: {0}");
                collection.Add( LogMessage.LOGMSG547,"Open {0}");
                collection.Add( LogMessage.LOGMSG548,"StartSnapshot() Snapshot already in progress.");
                collection.Add( LogMessage.LOGMSG549,"Creating new snapshot file and rolling older ones to higher number.");
                collection.Add( LogMessage.LOGMSG550,"SnapshotHandler()");
                collection.Add( LogMessage.LOGMSG551,"Snapshot writing Local Sequence  {0}, Remote Sequence {1}");
                collection.Add( LogMessage.LOGMSG552,"Snapshot found order by Id: {0}");
                collection.Add( LogMessage.LOGMSG553,"Snapshot found order by serial: {0}");
                collection.Add( LogMessage.LOGMSG554,"Snapshot writing unique order: {0}");
                collection.Add( LogMessage.LOGMSG555,"Symbol Positions:{0}");
                collection.Add( LogMessage.LOGMSG556,"Wrote snapshot. Sequence Remote = {0}, Local = {1}, Size = {2}. File Size = {3}");
                collection.Add( LogMessage.LOGMSG557,"Closed {0}");
                collection.Add( LogMessage.LOGMSG558,"Attempting recovery from snapshot file: {0}");
                collection.Add( LogMessage.LOGMSG559,"Trying snapshot at offset: {0}, length: {1}");
                collection.Add( LogMessage.LOGMSG560,"Snapshot successfully loaded.");
                collection.Add( LogMessage.LOGMSG561,"Clearing all orders.");
                collection.Add( LogMessage.LOGMSG562,"ForceSnapshot() - snapshot in progress. Waiting before beginning another snapshot...");
                collection.Add( LogMessage.LOGMSG563,"ForceSnapshot() - snapshot already started. Waiting before beginning another snapshot...");
                collection.Add( LogMessage.LOGMSG564,"ForceSnapshot() - starting snapshot now...");
                collection.Add( LogMessage.LOGMSG565,"ForceSnapshot() - snapshot complete.");
                collection.Add( LogMessage.LOGMSG566,"Synthetic order rejected: {0} {1}");
                collection.Add( LogMessage.LOGMSG567,"Sent quote for {0}: {1}");
                collection.Add( LogMessage.LOGMSG568,"Sent trade tick for {0}: {1}");
                collection.Add( LogMessage.LOGMSG569,"Clearing synthetic fill simulator.");
                collection.Add( LogMessage.LOGMSG570,"Freed box id in verify {0}, count {1}");
                collection.Add( LogMessage.LOGMSG571,"Verify");
                collection.Add( LogMessage.LOGMSG572,"Received a tick {0} UTC {1}");
                collection.Add( LogMessage.LOGMSG573,"Wait");
                collection.Add( LogMessage.LOGMSG574,"VerifyState symbol {0}, timeout {1}");
                collection.Add( LogMessage.LOGMSG575,"Received tick {0}");
                collection.Add( LogMessage.LOGMSG576,"VerifyState broker {0}, symbol {1}, timeout {2}");
                collection.Add( LogMessage.LOGMSG577,"VerifyEvent");
                collection.Add( LogMessage.LOGMSG578,"VerifyFeed");
                collection.Add( LogMessage.LOGMSG579,"Received tick #{0} {1} UTC {2}");
                collection.Add( LogMessage.LOGMSG580,"Clearing out tick #{0} {1} UTC {2}");
                collection.Add( LogMessage.LOGMSG581,"OnInitialize()");
                collection.Add( LogMessage.LOGMSG582,"Bar={0}, {1}");
                collection.Add( LogMessage.LOGMSG583,"{0}.Initialize()");
                collection.Add( LogMessage.LOGMSG584,"{0}, Bar={1}, {2}");
                collection.Add( LogMessage.LOGMSG585,"{0}.TargetProfit({1})");
                collection.Add( LogMessage.LOGMSG586,"ProcessFill: {0} for strategy {1}");
                collection.Add( LogMessage.LOGMSG587,"Matched fill with orderId: {0}");
                collection.Add( LogMessage.LOGMSG588,"Skipping fill, strategy order fills disabled.");
                collection.Add( LogMessage.LOGMSG589,"Skipping fill, exit strategy orders fills disabled.");
                collection.Add( LogMessage.LOGMSG590,"Changed strategy position to {0} because of fill.");
                collection.Add( LogMessage.LOGMSG591,"Filling {0} at {1} using tick UTC time {2}.{3}");
                collection.Add( LogMessage.LOGMSG592,"Filling {0} with {1} at ask {2} / bid {3} at {4}");
                collection.Add( LogMessage.LOGMSG593,"OnOpen({0})");
                collection.Add( LogMessage.LOGMSG594,"OnChangeBrokerOrder( {0})");
                collection.Add( LogMessage.LOGMSG595,"PhysicalOrder too late to change. Already filled or canceled, ignoring.");
                collection.Add( LogMessage.LOGMSG596,"Added order {0}");
                collection.Add( LogMessage.LOGMSG597,"Skipping TriggerCallback because HasCurrentTick is {0}");
                collection.Add( LogMessage.LOGMSG598,"Canceling by id {0}. Order: {1}");
                collection.Add( LogMessage.LOGMSG599,"OnCreateBrokerOrder( {0})");
                collection.Add( LogMessage.LOGMSG600,"OnCancelBrokerOrder( {0})");
                collection.Add( LogMessage.LOGMSG601,"Skipping ProcessOrders because HasCurrentTick is {0}");
                collection.Add( LogMessage.LOGMSG602,"StartTick({0})");
                collection.Add( LogMessage.LOGMSG603,"ProcessAdjustments( {0}, {1} )");
                collection.Add( LogMessage.LOGMSG604,"ProcessOrders( {0}, {1} ) [OpenTick]");
                collection.Add( LogMessage.LOGMSG605,"ProcessOrders( {0}, {1} )");
                collection.Add( LogMessage.LOGMSG606,"Orders: Touch {0}, Market {1}, Increase {2}, Decrease {3}");
                collection.Add( LogMessage.LOGMSG607,"Unable to flush fill queue yet because isOnline is {0}");
                collection.Add( LogMessage.LOGMSG608,"Dequeuing fill ( isOnline {0}): {1}");
                collection.Add( LogMessage.LOGMSG609,"Dequeuing reject {0}");
                collection.Add( LogMessage.LOGMSG610,"Found {0} open orders for {1}:");
                collection.Add( LogMessage.LOGMSG611,"{0}");
                collection.Add( LogMessage.LOGMSG612,"   {0} {1}");
                collection.Add( LogMessage.LOGMSG613,"    {0}");
                collection.Add( LogMessage.LOGMSG614,"Rejecting order because position is {0} but order side was {1}: {2}");
                collection.Add( LogMessage.LOGMSG615,"Filling order: {0}");
                collection.Add( LogMessage.LOGMSG616,"True Partial of only {0} fills out of {1} for {2}");
                collection.Add( LogMessage.LOGMSG617,"{0} totalSize {1}, split {2}, last {3}, cumul {4}, fills {5}, remain {6}");
                collection.Add( LogMessage.LOGMSG618,"Changing actual position from {0} to {1}. Fill size is {2}");
                collection.Add( LogMessage.LOGMSG619,"Enqueuing fill (online: {0}): {1}");
                collection.Add( LogMessage.LOGMSG620,"Setter: ActualPosition changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG621,"IsOnline changed to {0}");
                collection.Add( LogMessage.LOGMSG622,"DYNAMIC SUPPORT:    {0}");
                collection.Add( LogMessage.LOGMSG623,"LOW:                {0}");
                collection.Add( LogMessage.LOGMSG624,"xLL:                {0}");
                collection.Add( LogMessage.LOGMSG625,"DYNAMIC RESISTANCE: {0}");
                collection.Add( LogMessage.LOGMSG626,"HIGH:               {0}");
                collection.Add( LogMessage.LOGMSG627,"xHH:                {0}");
                collection.Add( LogMessage.LOGMSG628,"------- TRO_DYNAMIC_SR2 ---------");
                collection.Add( LogMessage.LOGMSG629,"rsi={0},wma={1},Value1={2},Value2={3},ifsh={4}");
                collection.Add( LogMessage.LOGMSG630,"{0}: price[0]={1},avgPrice[0]={2},price[1]={3},avgPrice[1]={4}");
                collection.Add( LogMessage.LOGMSG631,"gain.Add( LogMessage.{0})");
                collection.Add( LogMessage.LOGMSG632,"loss.Add( LogMessage.0)");
                collection.Add( LogMessage.LOGMSG633,"gain.Add( LogMessage.0)");
                collection.Add( LogMessage.LOGMSG634,"loss.Add( LogMessage.{0})");
                collection.Add( LogMessage.LOGMSG635,"{0}: BarCount={1},ag={2},al={3},rs={4},x={5},this={6}");
                collection.Add( LogMessage.LOGMSG636,"{0},{1},{2},{3},{4},{5},{6},{7}");
                collection.Add( LogMessage.LOGMSG637,"{0} waking up.");
                collection.Add( LogMessage.LOGMSG638,"{0} going to sleep.");
                collection.Add( LogMessage.LOGMSG639,"{0}.OnProperties() - NotImplemented");
                collection.Add( LogMessage.LOGMSG640,"Configuring Portfolio {0} for sub strategies/portfolios..");
                collection.Add( LogMessage.LOGMSG641,"TryMergeEquity for {0}");
                collection.Add( LogMessage.LOGMSG642,"Watcher {0} position={1}");
                collection.Add( LogMessage.LOGMSG643,"Resulting position={0}");
                collection.Add( LogMessage.LOGMSG644,"{0} {1}, type = {2}");
                collection.Add( LogMessage.LOGMSG645,"new");
                collection.Add( LogMessage.LOGMSG646,"Constructor");
                collection.Add( LogMessage.LOGMSG647,"Order #{0} was modified while position = {1}\n{2}");
                collection.Add( LogMessage.LOGMSG648,"Setting using {0} of {1}");
                collection.Add( LogMessage.LOGMSG649,"UpdatePrice( max = {0})");
                collection.Add( LogMessage.LOGMSG650,"UpdatePrice( min = {0})");
                collection.Add( LogMessage.LOGMSG651,"Enter long volume = {0}, short volume = {1}");
                collection.Add( LogMessage.LOGMSG652,"Exit long volume = {0}, short volume = {1}, Direction = {2}");
                collection.Add( LogMessage.LOGMSG653,"Price = {0}, averageEntryPrice = {1}, CurrentPosition = {2}, NewSize = {3}, Direction = {4}, sizeChange = {5}, Long volume = {6}, short volume = {7}");
                collection.Add( LogMessage.LOGMSG654,"Position change from {0} to {1}");
                collection.Add( LogMessage.LOGMSG655,"Loading {0}");
                collection.Add( LogMessage.LOGMSG656,"Frozen flag changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG657,"created with binary symbol id = {0}");
                collection.Add( LogMessage.LOGMSG658,"ForceClear({0}) {1}");
                collection.Add( LogMessage.LOGMSG659,"AddTick({0}) {1}");
                collection.Add( LogMessage.LOGMSG660,"RemoveTick({0},{1}) {2}");
                collection.Add( LogMessage.LOGMSG661,"Tick counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG662,"AddPhysicalFill( Created {0}, Waiting {1}, Fill {2}) {3}");
                collection.Add( LogMessage.LOGMSG663,"RemovePhysicalFill( Created {0}, Waiting {1}, {2}) {3}");
                collection.Add( LogMessage.LOGMSG664,"physicalFillsCreated counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG665,"physicalFillsWaiting counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG666,"RemovePhysicalFillWaiting( Waiting {0}, {1}) {2}");
                collection.Add( LogMessage.LOGMSG667,"AddOrderChange({0}) {1}");
                collection.Add( LogMessage.LOGMSG668,"RemoveOrderChange({0}) {1}");
                collection.Add( LogMessage.LOGMSG669,"OrderChange counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG670,"AddPhysicalOrder({0},{1}) {2}");
                collection.Add( LogMessage.LOGMSG671,"RemovePhysicalOrder({0},{1}) {2}");
                collection.Add( LogMessage.LOGMSG672,"PhysicalOrders counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG673,"RemovePhysicalOrder({0}) {1}");
                collection.Add( LogMessage.LOGMSG674,"SetSwitchBrokerState({0}, {1}) {2}");
                collection.Add( LogMessage.LOGMSG675,"ClearSwitchBrokerState({0},{1}) {2}");
                collection.Add( LogMessage.LOGMSG676,"SwitchBrokerState counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG677,"AddPositionChange({0}, {1}) {2}");
                collection.Add( LogMessage.LOGMSG678,"RemovePositionChange({0},{1}) {2}");
                collection.Add( LogMessage.LOGMSG679,"PositionChange counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG680,"AddWaitingMatch({0}, {1}) {2}");
                collection.Add( LogMessage.LOGMSG681,"RemoveWaitingMatch({0},{1}) {2}");
                collection.Add( LogMessage.LOGMSG682,"WaitingMatch counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG683,"AddProcessPhysicalOrders({0}) {1}");
                collection.Add( LogMessage.LOGMSG684,"RemoveProcessPhysicalOrders({0}) {1}");
                collection.Add( LogMessage.LOGMSG685,"ProcessPhysical counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG686,"SetReprocessPhysicalOrders({0}) {1}");
                collection.Add( LogMessage.LOGMSG687,"ClearReprocessPhysicalOrders({0}) {1}");
                collection.Add( LogMessage.LOGMSG688,"ReprocessPhysical counter was {0}. Incremented to {1}");
                collection.Add( LogMessage.LOGMSG689,"New StrategyPosition");
                collection.Add( LogMessage.LOGMSG690,"SetExpectedPosition() strategy {0} for {1} position change from {2} to {3}.");
                collection.Add( LogMessage.LOGMSG691,"Strategy {0} for {1} actual position changed from {2} to {3}.");
                collection.Add( LogMessage.LOGMSG692,"Unchanged strategy {0} for {1}. Actual position {2}.");
                collection.Add( LogMessage.LOGMSG693,"new ");
                collection.Add( LogMessage.LOGMSG694,"{0} InsertBefore() {1} before {2}");
                collection.Add( LogMessage.LOGMSG695,"{0} Replace() {1} with {2}");
                collection.Add( LogMessage.LOGMSG696,"{0} InsertAfter() {1} after {2}");
                collection.Add( LogMessage.LOGMSG697,"TickZoomProfiler load ERROR: {0}");
                collection.Add( LogMessage.LOGMSG698,"Registered {0} metric ({1}) on tick {2})");
                collection.Add( LogMessage.LOGMSG699,"Started background worker.");
                collection.Add( LogMessage.LOGMSG700,"Downloading {0} to {1}");
                collection.Add( LogMessage.LOGMSG701,"Post to {0} with parameters = {1}");
                collection.Add( LogMessage.LOGMSG702,"StartCoreServices()");
                collection.Add( LogMessage.LOGMSG703,"Plugin");
                collection.Add( LogMessage.LOGMSG704,"HistoricalShowChart() start.");
                collection.Add( LogMessage.LOGMSG705,"HistoricalShowChart() finished.");
                collection.Add( LogMessage.LOGMSG706,"bar {0} is missing");
                collection.Add( LogMessage.LOGMSG707,"bar: {0}, point: {1} {2} days:{3},{4},{5},{6}");
                collection.Add( LogMessage.LOGMSG708,"bar: {0}, point: {1} {2} days:{3} {4}");
                collection.Add( LogMessage.LOGMSG709,"close: {0} {1} {2}");
                collection.Add( LogMessage.LOGMSG710,"isFlat {0}, Position.IsFlat {1}, trades.Count {2}, Completed {3}");
                collection.Add( LogMessage.LOGMSG711,"Close {0}, Open {1}");
                collection.Add( LogMessage.LOGMSG712,"Long: Beginning {0}, break even {1}, min price {2}, bid {3}, offer {4}, position {5}");
                collection.Add( LogMessage.LOGMSG713,"Short: Beginning {0}, break even {1}, max price {2}, bid {3}/ offer {4}, position {5}, market bid {6}/ offer {7}");
                collection.Add( LogMessage.LOGMSG714,"Changed {0} at {1}, position {2}");
                collection.Add( LogMessage.LOGMSG715,"OnEnterTrade() completed={0} {1}");
                collection.Add( LogMessage.LOGMSG716,"OnChangeTrade() completed={0} {1}");
                collection.Add( LogMessage.LOGMSG717,"OnExitTrade completed={0} {1}");
                collection.Add( LogMessage.LOGMSG718,"{0} fills");
                collection.Add( LogMessage.LOGMSG719,"Fill: {0} at {1} {2}");
                collection.Add( LogMessage.LOGMSG720,"Low Max Volume Set to {0}");
                collection.Add( LogMessage.LOGMSG721,"LogShortTraverse={0}, volume={1}, low max volume={2}");
                collection.Add( LogMessage.LOGMSG722,"|Pivot High,{0},{1},{2},{3}");
                collection.Add( LogMessage.LOGMSG723,"|Pivot Low,{0},{1},{2},{3}");
                collection.Add( LogMessage.LOGMSG724,"ChartLoad()");
                collection.Add( LogMessage.LOGMSG725,"ChartResize()");
                collection.Add( LogMessage.LOGMSG726,"AddBar()");
                collection.Add( LogMessage.LOGMSG727,"UpdateTick()");
                collection.Add( LogMessage.LOGMSG728,"AddBarPrivate()");
                collection.Add( LogMessage.LOGMSG729,"yMax is NAN from MoveByPixels with yScale {0}, yScale.Max {1}, resetYScaleSpeed {2}, resetYScale {3}");
                collection.Add( LogMessage.LOGMSG730,"yMin is NAN from MoveByPixels with yScale {0}, yScale.Min {1}, resetYScaleSpeed {2}, resetYScale {3}");
                collection.Add( LogMessage.LOGMSG731,"_min is NAN from MoveByPixels with xScale {0}, xScale.Min {1}, resetXScaleSpeed {2}");
                collection.Add( LogMessage.LOGMSG732,"_max is NAN from MoveByPixels with xScale {0}, xScale.Max {1}, resetXScaleSpeed {2}");
                collection.Add( LogMessage.LOGMSG733,"CreateObjects()");
                collection.Add( LogMessage.LOGMSG734,"refreshTick()");
                collection.Add( LogMessage.LOGMSG735,"dragging={0}, AutoScroll = {1}");
                collection.Add( LogMessage.LOGMSG736,"refreshing axis");
                collection.Add( LogMessage.LOGMSG737,"redrawing");
                collection.Add( LogMessage.LOGMSG738,"ThreadStatic = {0}");
                collection.Add( LogMessage.LOGMSG739,"Static = {0}");
                collection.Add( LogMessage.LOGMSG740,"Dequeue test = {0}");
                collection.Add( LogMessage.LOGMSG741,"TickQueue Dequeue (Structs) = {0}");
                collection.Add( LogMessage.LOGMSG742,"TickQueue (ticks) = {0}");
                collection.Add( LogMessage.LOGMSG743,"TickQueue = {0}");
                collection.Add( LogMessage.LOGMSG744,"W/O Perf Counter = {0}");
                collection.Add( LogMessage.LOGMSG745,"With Perf Reset = {0}");
                collection.Add( LogMessage.LOGMSG746,"With Perf Elapsed = {0}");
                collection.Add( LogMessage.LOGMSG747,"Delegate = {0}");
                collection.Add( LogMessage.LOGMSG748,"Method = {0}");
                collection.Add( LogMessage.LOGMSG749,"RollingByteTest elapsed = {0}");
                collection.Add( LogMessage.LOGMSG750,"==== Dispose() =====");
                collection.Add( LogMessage.LOGMSG751,"==== Setup() =====");
                collection.Add( LogMessage.LOGMSG752,"==== TearDown() =====");
                collection.Add( LogMessage.LOGMSG753,"===VerifyFeed===");
                collection.Add( LogMessage.LOGMSG754,"===StopSymbol===");
                collection.Add( LogMessage.LOGMSG755,"===StartSymbol===  lastTick {0}");
                collection.Add( LogMessage.LOGMSG756,"===CountTicks===");
                collection.Add( LogMessage.LOGMSG757,"Queue Terminated");
                collection.Add( LogMessage.LOGMSG758,"Latency count = {0}");
                collection.Add( LogMessage.LOGMSG759,"{0}: {1}, {2}, {3}, {4} - {5}");
                collection.Add( LogMessage.LOGMSG760,"Totals:  {0}, {1}, {2}, {3}");
                collection.Add( LogMessage.LOGMSG761,"Averages:  {0}, {1}, {2}, {3}");
                collection.Add( LogMessage.LOGMSG762,"{0} before, {1} during, and {2} after timings were over 500 microseconds.");
                collection.Add( LogMessage.LOGMSG763,"Sent message at {0}");
                collection.Add( LogMessage.LOGMSG764,"Received message at {0}, sent {1}, message snd {2}, message rcv {3}");
                collection.Add( LogMessage.LOGMSG765,"Created with capacity {0}");
                collection.Add( LogMessage.LOGMSG766,"Flush called");
                collection.Add( LogMessage.LOGMSG767,"Created");
                collection.Add( LogMessage.LOGMSG768,"AddSocket({0})");
                collection.Add( LogMessage.LOGMSG769,"Current list of sockets:\n{0}");
                collection.Add( LogMessage.LOGMSG770,"RemoveReader({0})");
                collection.Add( LogMessage.LOGMSG771,"RemoveWriter({0})");
                collection.Add( LogMessage.LOGMSG772,"Select: {0} sockets active.");
                collection.Add( LogMessage.LOGMSG773,"Found {0} sockets ready to read.");
                collection.Add( LogMessage.LOGMSG774,"Ready to read on socket: {0}");
                collection.Add( LogMessage.LOGMSG775,"Graceful closing for {0}");
                collection.Add( LogMessage.LOGMSG776,"Graceful shutdown on read for {0}");
                collection.Add( LogMessage.LOGMSG777,"A connection was lost for {0}");
                collection.Add( LogMessage.LOGMSG778,"Found {0} sockets ready to write.");
                collection.Add( LogMessage.LOGMSG779,"Ready to write on socket: {0}");
                collection.Add( LogMessage.LOGMSG780,"Shutdown()");
                collection.Add( LogMessage.LOGMSG781,"None {0}, Pnd {1}, Con {2}, Bnd {3}, Lst {4}");
                collection.Add( LogMessage.LOGMSG782,"removing selector from list");
                collection.Add( LogMessage.LOGMSG783,"Listen for {0}");
                collection.Add( LogMessage.LOGMSG784,"Binding handle {0} to port {1}");
                collection.Add( LogMessage.LOGMSG785,"Bound handle {0} to port {1}");
                collection.Add( LogMessage.LOGMSG786,"AlreadyInUse port {0} by handle {1}");
                collection.Add( LogMessage.LOGMSG787,"After closing handle for {0}");
                collection.Add( LogMessage.LOGMSG788,"Startup");
                collection.Add( LogMessage.LOGMSG789,"Shutdown");
                collection.Add( LogMessage.LOGMSG790,"OnConnect() {0}");
                collection.Add( LogMessage.LOGMSG791,"OnDisconnection({0})");
                collection.Add( LogMessage.LOGMSG792,"Shared memory socket {0} created with port {1}");
                collection.Add( LogMessage.LOGMSG793,"Socket created. Handle = {0}");
                collection.Add( LogMessage.LOGMSG794,"Creating receiveMessage buffer on port: {0}");
                collection.Add( LogMessage.LOGMSG795,"Attempting bind to shared memory port: {0}");
                collection.Add( LogMessage.LOGMSG796,"Close() {0}");
                collection.Add( LogMessage.LOGMSG797,"CloseServer()");
                collection.Add( LogMessage.LOGMSG798,"TrySendClosed()");
                collection.Add( LogMessage.LOGMSG799,"calling OnSelectWrite()");
                collection.Add( LogMessage.LOGMSG800,"Already closed connection for handle {0} because {1}");
                collection.Add( LogMessage.LOGMSG801,"Listen {0}");
                collection.Add( LogMessage.LOGMSG802,"OnAccept()");
                collection.Add( LogMessage.LOGMSG803,"Adding message {0} with time {1} to send queue for {2}. Now {3} send items.");
                collection.Add( LogMessage.LOGMSG804,"readBuffer.Position {0}, readBuffer.Length {1}");
                collection.Add( LogMessage.LOGMSG805,"Found position {0}, remaining {1}");
                collection.Add( LogMessage.LOGMSG806,"Before ReceiveRaw: length {0}, readBuffer.Position {1}, readBuffer.Length {2}, capacityRemaining {3}");
                collection.Add( LogMessage.LOGMSG807,"After ReceiveRaw: length {0}, readBuffer.Position {1}, readBuffer.Length {2}");
                collection.Add( LogMessage.LOGMSG808,"Attempt to add message to receive queue: {0}");
                collection.Add( LogMessage.LOGMSG809,"ReceiveQueue was full on port {0}");
                collection.Add( LogMessage.LOGMSG810,"Sent message to receive queue: {0}");
                collection.Add( LogMessage.LOGMSG811,"Sending message counter {0}");
                collection.Add( LogMessage.LOGMSG812,"Writing Message to buffer: {0}");
                collection.Add( LogMessage.LOGMSG813,"OnSelectWrite: written: {0}, remaining = {1}");
                collection.Add( LogMessage.LOGMSG814,"Connect to shared memory port {0}");
                collection.Add( LogMessage.LOGMSG815,"Connected!");
                collection.Add( LogMessage.LOGMSG816,"Waiting for asynchronous connection.");
                collection.Add( LogMessage.LOGMSG817,"calling closesocket for: {0}");
                collection.Add( LogMessage.LOGMSG818,"Assigning new MessageFactory");
                collection.Add( LogMessage.LOGMSG819,"Socket created: {0}, {1}, {2} for {3}");
                collection.Add( LogMessage.LOGMSG820,"Socket handle = {0}");
                collection.Add( LogMessage.LOGMSG821,"Attempting bind to address: {0} port: {1}");
                collection.Add( LogMessage.LOGMSG822,"Found {0} ip addresses. Taking the first.");
                collection.Add( LogMessage.LOGMSG823,"Found empty address {0} so default binds to all interfaces on the machine.");
                collection.Add( LogMessage.LOGMSG824,"Finish( {0})");
                collection.Add( LogMessage.LOGMSG825,"ShutdownWrite()");
                collection.Add( LogMessage.LOGMSG826,"TryReadClosed()");
                collection.Add( LogMessage.LOGMSG827,"calling OnSelectRead()");
                collection.Add( LogMessage.LOGMSG828,"Closing a connection for handle {0} because {1}");
                collection.Add( LogMessage.LOGMSG829,"calling shutdown for: {0}");
                collection.Add( LogMessage.LOGMSG830,"Writing Message to buffer (socket {0}): {1}");
                collection.Add( LogMessage.LOGMSG831,"Connect to {0}:{1}");
                collection.Add( LogMessage.LOGMSG832,"Found {0} ip addresses to try.");
                collection.Add( LogMessage.LOGMSG833,"state changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG834,"Get Serializer for {0}");
                collection.Add( LogMessage.LOGMSG835,"Spawning ProviderManager");
                collection.Add( LogMessage.LOGMSG836,"Sending Connect event to ProviderManager");
                collection.Add( LogMessage.LOGMSG837,"Start({0})");
                collection.Add( LogMessage.LOGMSG838,"StartSymbol: Sending request for SymbolManager to ProviderManager.");
                collection.Add( LogMessage.LOGMSG839,"StartSymbol: Sending SymbolManager (request) to SymbolManager.");
                collection.Add( LogMessage.LOGMSG840,"StopSymbol: Sending request for SymbolManager to ProviderManager.");
                collection.Add( LogMessage.LOGMSG841,"CustomEvent {0}");
                collection.Add( LogMessage.LOGMSG842,"Disconnecting from all SymbolManagers");
                collection.Add( LogMessage.LOGMSG843,"OnStart()");
                collection.Add( LogMessage.LOGMSG844,"OnStop()");
                collection.Add( LogMessage.LOGMSG845,"Socket selector started.");
                collection.Add( LogMessage.LOGMSG846,"OnClientConnect()");
                collection.Add( LogMessage.LOGMSG847,"OnClientDisconnect({0})");
                collection.Add( LogMessage.LOGMSG848,"Stop()");
                collection.Add( LogMessage.LOGMSG849,"Loading from config file: {0}");
                collection.Add( LogMessage.LOGMSG850,"GetSymbolManager()");
                collection.Add( LogMessage.LOGMSG851,"Replying with symbol manager: {0}");
                collection.Add( LogMessage.LOGMSG852,"Starting the data provider");
                collection.Add( LogMessage.LOGMSG853,"Starting the execution provider");
                collection.Add( LogMessage.LOGMSG854,"Connect(Agent)");
                collection.Add( LogMessage.LOGMSG855,"StartSymbol({0})");
                collection.Add( LogMessage.LOGMSG856,"StartBroker({0})");
                collection.Add( LogMessage.LOGMSG857,"StopSymbol({0})");
                collection.Add( LogMessage.LOGMSG858,"Disconnect(Agent)");
                collection.Add( LogMessage.LOGMSG859,"PositionChange: {0}");
                collection.Add( LogMessage.LOGMSG860,"SendConfigurationResponse: {0}");
                collection.Add( LogMessage.LOGMSG861,"Config file changed: {0}");
                collection.Add( LogMessage.LOGMSG862,"{0}({1})");
                collection.Add( LogMessage.LOGMSG863,"Connection Monitor Started");
                collection.Add( LogMessage.LOGMSG864,"HeartbeatTimerEvent");
                collection.Add( LogMessage.LOGMSG865,"Retrying after {0}ms");
                collection.Add( LogMessage.LOGMSG866,"Adding StartSymbol request for {0}, isSent {1}");
                collection.Add( LogMessage.LOGMSG867,"Socket state {0}");
                collection.Add( LogMessage.LOGMSG868,"StopSymbol {0}");
                collection.Add( LogMessage.LOGMSG869,"PositionChange( {0})");
                collection.Add( LogMessage.LOGMSG870,"Syncing Strategy Position: {0}");
                collection.Add( LogMessage.LOGMSG871,"SendRemoteShutdown()");
                collection.Add( LogMessage.LOGMSG872,"CustomEvent( {0}, {1})");
                collection.Add( LogMessage.LOGMSG873,"Sent CustomEvent( {0}, {1})");
                collection.Add( LogMessage.LOGMSG874,"OnConnect( {0})");
                collection.Add( LogMessage.LOGMSG875,"OnDisconnect( {0})");
                collection.Add( LogMessage.LOGMSG876,"Stopping monitorTask");
                collection.Add( LogMessage.LOGMSG877,"Attempting connection to {0}, {1}...");
                collection.Add( LogMessage.LOGMSG878,"Connected ProviderProxy with {0}");
                collection.Add( LogMessage.LOGMSG879,"Starting ProviderProxy Task");
                collection.Add( LogMessage.LOGMSG880,"Disconnect( Agent): {0}");
                collection.Add( LogMessage.LOGMSG881,"CloseSocket():{0}");
                collection.Add( LogMessage.LOGMSG882,"Closing {0}");
                collection.Add( LogMessage.LOGMSG883,"Checking for socket before closing: {0}");
                collection.Add( LogMessage.LOGMSG884,"Socket has {0} messages to send: {1} acks waiting: ");
                collection.Add( LogMessage.LOGMSG885,"Waiting for remote shutdown.");
                collection.Add( LogMessage.LOGMSG886,"Dispose():{0}");
                collection.Add( LogMessage.LOGMSG887,"Finished Dispose():{0}");
                collection.Add( LogMessage.LOGMSG888,"ProcessStartup");
                collection.Add( LogMessage.LOGMSG889,"Sending StartSymbol {0}, {1}");
                collection.Add( LogMessage.LOGMSG890,"Read message from queue: {0}");
                collection.Add( LogMessage.LOGMSG891,"ProcessMessage at position {0} on message {1}");
                collection.Add( LogMessage.LOGMSG892,"Received a heartbeat: {0}");
                collection.Add( LogMessage.LOGMSG893,"Reading tick from message: {0}");
                collection.Add( LogMessage.LOGMSG894,"Received tick {0} #{1} {2}");
                collection.Add( LogMessage.LOGMSG895,"Received Error Event");
                collection.Add( LogMessage.LOGMSG896,"Received OnCustomEvent {0}");
                collection.Add( LogMessage.LOGMSG897,"Attempting to send tick: {0}");
                collection.Add( LogMessage.LOGMSG898,"Sending tick to Agent {0}");
                collection.Add( LogMessage.LOGMSG899,"Symbol Request entry not found so skipping tick: {0}");
                collection.Add( LogMessage.LOGMSG900,"LogicalFill({0},{1})");
                collection.Add( LogMessage.LOGMSG901,"OnCustomEvent {0}({1},{2})");
                collection.Add( LogMessage.LOGMSG902,"StartRealTime({0}) :{1}");
                collection.Add( LogMessage.LOGMSG903,"EndRealTime({0}) :{1}");
                collection.Add( LogMessage.LOGMSG904,"StartBroker({0}) :{1}");
                collection.Add( LogMessage.LOGMSG905,"No symbol request for {0}. So StartBroker was not sent.");
                collection.Add( LogMessage.LOGMSG906,"EndBroker({0}) :{1}");
                collection.Add( LogMessage.LOGMSG907,"StartHistorical({0}) :{1}");
                collection.Add( LogMessage.LOGMSG908,"EndHistorical({0}) :{1}");
                collection.Add( LogMessage.LOGMSG909,"Agent was terminated.");
                collection.Add( LogMessage.LOGMSG910,"Thread exiting");
                collection.Add( LogMessage.LOGMSG911,"Adding expected ack for {0} with counter {1}");
                collection.Add( LogMessage.LOGMSG912,"ProviderStub on socket: {0}");
                collection.Add( LogMessage.LOGMSG913,"Created with {0}");
                collection.Add( LogMessage.LOGMSG914,"Sending tick {0} #{1} {2}");
                collection.Add( LogMessage.LOGMSG915,"Sent OnError ");
                collection.Add( LogMessage.LOGMSG916,"Stopping heartbeat task.");
                collection.Add( LogMessage.LOGMSG917,"Canceling heartbeat timer.");
                collection.Add( LogMessage.LOGMSG918,"ProcessStartSymbol");
                collection.Add( LogMessage.LOGMSG919,"Received StartSymbol {0}");
                collection.Add( LogMessage.LOGMSG920,"Received StopSymbol Message");
                collection.Add( LogMessage.LOGMSG921,"Symbol = {0}");
                collection.Add( LogMessage.LOGMSG922,"Closing connection {0}");
                collection.Add( LogMessage.LOGMSG923,"Sent Heartbeat.");
                collection.Add( LogMessage.LOGMSG924,"Sending ack {0} for Message counter {1}");
                collection.Add( LogMessage.LOGMSG925,"Sent {0} ack.");
                collection.Add( LogMessage.LOGMSG926,"Received PositionChange {0}");
                collection.Add( LogMessage.LOGMSG927,"Sent BeginHistorical {0} :{1}");
                collection.Add( LogMessage.LOGMSG928,"Sent OnEndHistorical {0} :{1}");
                collection.Add( LogMessage.LOGMSG929,"Sent BeginRealTime {0} :{1}");
                collection.Add( LogMessage.LOGMSG930,"Sent RemoteShutdown :{0}");
                collection.Add( LogMessage.LOGMSG931,"Socket receive queue count {0}, send queue count {1}");
                collection.Add( LogMessage.LOGMSG932,"Waiting for socket receive queue to flush {0} messages: {1}");
                collection.Add( LogMessage.LOGMSG933,"Finsihed flushing socket.");
                collection.Add( LogMessage.LOGMSG934,"Waiting for inbound queue to flush {0} messages: {1}");
                collection.Add( LogMessage.LOGMSG935,"Finsihed flushing inbound queues.");
                collection.Add( LogMessage.LOGMSG936,"Last Tick Time Sent was {0}");
                collection.Add( LogMessage.LOGMSG937,"Sent OnEndRealTime {0} :{1}");
                collection.Add( LogMessage.LOGMSG938,"Sent StartBroker {0} :{1}");
                collection.Add( LogMessage.LOGMSG939,"Sent OnEndBroker {0} :{1}");
                collection.Add( LogMessage.LOGMSG940,"Sent OnPosition.");
                collection.Add( LogMessage.LOGMSG941,"Sent OnCustomEvent {0} for {1}");
                collection.Add( LogMessage.LOGMSG942,"OnFinalized()");
                collection.Add( LogMessage.LOGMSG943,"Attempting to kill any running process with name {0}");
                collection.Add( LogMessage.LOGMSG944,"SendDataEvent {0}");
                collection.Add( LogMessage.LOGMSG945,"Can't send {0} because back fill is in progress.");
                collection.Add( LogMessage.LOGMSG946,"Sending event {0} for {1}");
                collection.Add( LogMessage.LOGMSG947,"QueueException returned {0}");
                collection.Add( LogMessage.LOGMSG948,"Freed in send update queue {0}, count {1}");
                collection.Add( LogMessage.LOGMSG949,"UpdateQueue {0} Item: {1}");
                collection.Add( LogMessage.LOGMSG950,"ReaderFinished()");
                collection.Add( LogMessage.LOGMSG951,"Freed box id in update queue {0}, count {1}");
                collection.Add( LogMessage.LOGMSG952,"Adding tick to update queue during back fill: {0}");
                collection.Add( LogMessage.LOGMSG953,"Sending tick to receiver queue in real time: {0}");
                collection.Add( LogMessage.LOGMSG954,"Destroy()");
                collection.Add( LogMessage.LOGMSG955,"Status changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG956,"Server Cache Folder = {0}");
                collection.Add( LogMessage.LOGMSG957,"Start(provider)");
                collection.Add( LogMessage.LOGMSG958,"StartSymbol({0}, last time {1})");
                collection.Add( LogMessage.LOGMSG959,"Provider already has StartSymbol.");
                collection.Add( LogMessage.LOGMSG960,"StartSymbol timeSync count = {0})");
                collection.Add( LogMessage.LOGMSG961,"SendDataEvent to all clients {0}");
                collection.Add( LogMessage.LOGMSG962,"Writing tick for: {0}");
                collection.Add( LogMessage.LOGMSG963,"Setting lastTickTime: {0}");
                collection.Add( LogMessage.LOGMSG964,"Rejected writing {0} tick. (Price: {1}) Tick time: {2}.{3}");
                collection.Add( LogMessage.LOGMSG965,"Skipping tick because no client connected to receive: {0}");
                collection.Add( LogMessage.LOGMSG966,"SymbolState changed to {0} from {1}");
                collection.Add( LogMessage.LOGMSG967,"Detached a client");
                collection.Add( LogMessage.LOGMSG968,"BrokerState changed to {0} from {1}");
                collection.Add( LogMessage.LOGMSG969,"Enqueue {0}");
                collection.Add( LogMessage.LOGMSG970,"IncreaseInbound with count {0}");
                collection.Add( LogMessage.LOGMSG971,"Dequeue {0}");
                collection.Add( LogMessage.LOGMSG972,"DecreaseInbound with count = {0}");
                collection.Add( LogMessage.LOGMSG973,"DecreaseOutbound with count {0}, previous count {1}");
                collection.Add( LogMessage.LOGMSG974,"{0} queue now cleared after backup to {1} items.");
                collection.Add( LogMessage.LOGMSG975,"Clear called");
                collection.Add( LogMessage.LOGMSG976,"Dispose({0})");
                collection.Add( LogMessage.LOGMSG977,"StartDequeue called");
                collection.Add( LogMessage.LOGMSG978,"Calling StartEnqueue");
                collection.Add( LogMessage.LOGMSG979,"Found spawn request for ProviderManager.");
                collection.Add( LogMessage.LOGMSG980,"Found already existing ProviderManager. Returning singleton.");
                collection.Add( LogMessage.LOGMSG981,"Process thread count: {0}");
                collection.Add( LogMessage.LOGMSG982,"ProcessThread {0}, priority {1}, cpu time {2}, start {3}");
                collection.Add( LogMessage.LOGMSG983,"Thread pool terminated.");
                collection.Add( LogMessage.LOGMSG984,"TheadPool thread exiting!");
                collection.Add( LogMessage.LOGMSG985,"ShutdownAgents");
                collection.Add( LogMessage.LOGMSG986,"Sending shutdown message to {0}");
                collection.Add( LogMessage.LOGMSG987,"Sending shutdown request to these tasks: ");
                collection.Add( LogMessage.LOGMSG988,"        {0}");
                collection.Add( LogMessage.LOGMSG989,"RECEIVED: {0}");
                collection.Add( LogMessage.LOGMSG990,"SENT on cross thread queue: {0}");
                collection.Add( LogMessage.LOGMSG991,"SENT on receiver queue: {0}");
                collection.Add( LogMessage.LOGMSG992,"Cannot connect queues to task when scheduler is {0} for {1}. Ignoring.");
                collection.Add( LogMessage.LOGMSG993,"Increased inbound for {0} to {1}");
                collection.Add( LogMessage.LOGMSG994,"Increased outbound for {0} to {1}");
                collection.Add( LogMessage.LOGMSG995,"Decreased inbound for {0} to {1}");
                collection.Add( LogMessage.LOGMSG996,"Decreased outbound for {0} to {1}");
                collection.Add( LogMessage.LOGMSG997,"{0} didn't pass VerifyOutbound() because sum is {1}");
                collection.Add( LogMessage.LOGMSG998,"Pause() status {0}, for {1}");
                collection.Add( LogMessage.LOGMSG999,"Pausing a task that was already paused. Task: {0}");
                collection.Add( LogMessage.LOGMSG1000,"Pausing task that was already stopped.");
                collection.Add( LogMessage.LOGMSG1001,"Resume() status {0}, for {1}");
                collection.Add( LogMessage.LOGMSG1002,"Resuming a task that was not paused.");
                collection.Add( LogMessage.LOGMSG1003,"Resuming a paused task.");
                collection.Add( LogMessage.LOGMSG1004,"Task is already stopped. Task: {0}");
                collection.Add( LogMessage.LOGMSG1005,"Schedule() status {0}, for {1}");
                collection.Add( LogMessage.LOGMSG1006,"Skipping scheduling a paused task.");
                collection.Add( LogMessage.LOGMSG1007,"Start() status {0}, for {1}");
                collection.Add( LogMessage.LOGMSG1008,"Stop() status {0}, for {1}");
                collection.Add( LogMessage.LOGMSG1009,"Removing {0}");
                collection.Add( LogMessage.LOGMSG1010,"ResortFirst {0}");
                collection.Add( LogMessage.LOGMSG1011,"Checking for duplicate for {0}");
                collection.Add( LogMessage.LOGMSG1012,"Adding task {0}");
                collection.Add( LogMessage.LOGMSG1013,"Removing task {0}");
                collection.Add( LogMessage.LOGMSG1014,"RESENDing from cross thread queue: {0}");
                collection.Add( LogMessage.LOGMSG1015,"Firing timer for {0} at {1}, set for {2}, latency {3}");
                collection.Add( LogMessage.LOGMSG1016,"Prevent sleep {0} at {1} while -timerDelta {2} < sliceInterval * 2 {3}");
                collection.Add( LogMessage.LOGMSG1017,"Collecting stack trace for: {0}");
                collection.Add( LogMessage.LOGMSG1018,"Start {0}:{1} at {2}");
                collection.Add( LogMessage.LOGMSG1019,"Cancel {0}:{1}");
                collection.Add( LogMessage.LOGMSG1020,"Dispose() {0}:{1}");
                collection.Add( LogMessage.LOGMSG1021,"Start BarSimulator.");
                collection.Add( LogMessage.LOGMSG1022,"FinishTick({0})");
                collection.Add( LogMessage.LOGMSG1023,"Changing fill from {0} to {1}");
                collection.Add( LogMessage.LOGMSG1024,"This engine was already disposed.");
                collection.Add( LogMessage.LOGMSG1025,"This engine was already finalized.");
                collection.Add( LogMessage.LOGMSG1026,"Updated tick count of {0} to {1}");
                collection.Add( LogMessage.LOGMSG1027,"Resetting first time out to max value.");
                collection.Add( LogMessage.LOGMSG1028,"===========   None of the symbol tick counts increased. ============== ");
                collection.Add( LogMessage.LOGMSG1029,"Timeout {0}, Elapsed {1}ms.");
                collection.Add( LogMessage.LOGMSG1030,"Controllers timed out.");
                collection.Add( LogMessage.LOGMSG1031,"Setting first time out {0}");
                collection.Add( LogMessage.LOGMSG1032,"===========   Symbol controllers timed out and all tick sync complete. ============== ");
                collection.Add( LogMessage.LOGMSG1033,"===========   Symbol controllers timed out. ============== ");
                collection.Add( LogMessage.LOGMSG1034,"Setting timeout for {0}");
                collection.Add( LogMessage.LOGMSG1035,"new TickEngine()");
                collection.Add( LogMessage.LOGMSG1036,"Dependency Discovery starting with {0}");
                collection.Add( LogMessage.LOGMSG1037,"TryCreateFirstCheckPointTick()");
                collection.Add( LogMessage.LOGMSG1038,"Chart.IsDynamicUpdate = true because market replay is enabled.");
                collection.Add( LogMessage.LOGMSG1039,"Creating chart for controller {0}");
                collection.Add( LogMessage.LOGMSG1040,"Created empty chart for controller {0}. Callback = {1}, and group = {2}");
                collection.Add( LogMessage.LOGMSG1041,"RecursiveInitialize({0})");
                collection.Add( LogMessage.LOGMSG1042,"Examine {0} for {1}");
                collection.Add( LogMessage.LOGMSG1043,"Add {0}");
                collection.Add( LogMessage.LOGMSG1044,"InitializeFormula({0})");
                collection.Add( LogMessage.LOGMSG1045,"A start tick was found.");
                collection.Add( LogMessage.LOGMSG1046,"VerifyControllersAsleep for {0} controllers.");
                collection.Add( LogMessage.LOGMSG1047,"ContinueControllerLoops()");
                collection.Add( LogMessage.LOGMSG1048," Pausing Engine task until controllers complete.");
                collection.Add( LogMessage.LOGMSG1049," controller next tick UTC time {0} greater or equal to check point time {1}");
                collection.Add( LogMessage.LOGMSG1050,"ResumeController({0})");
                collection.Add( LogMessage.LOGMSG1051," -- > continuing controller {0}");
                collection.Add( LogMessage.LOGMSG1052,"Checkpoints {0}: Adding checkpoint entry for {1}");
                collection.Add( LogMessage.LOGMSG1053,"Removed last check point controller. Ready for next check point.");
                collection.Add( LogMessage.LOGMSG1054,"Found first tick controller at {0} UTC");
                collection.Add( LogMessage.LOGMSG1055,"Check point set to {0} UTC");
                collection.Add( LogMessage.LOGMSG1056,"Started test finished timer for {0}");
                collection.Add( LogMessage.LOGMSG1057,"End Tick Loop");
                collection.Add( LogMessage.LOGMSG1058,"Engine canceled.");
                collection.Add( LogMessage.LOGMSG1059,"Writing summary data.");
                collection.Add( LogMessage.LOGMSG1060,"Writing performance stats.");
                collection.Add( LogMessage.LOGMSG1061,"EndTickLoop invoking Stop()");
                collection.Add( LogMessage.LOGMSG1062,"Last check point completed. Resuming Engine task.");
                collection.Add( LogMessage.LOGMSG1063,"ControllerCheckPoint() count = {0}");
                collection.Add( LogMessage.LOGMSG1064,"ControllerEndHistorical: Last check point completed.");
                collection.Add( LogMessage.LOGMSG1065,"These symbols remain to finish historical: {0}");
                collection.Add( LogMessage.LOGMSG1066,"{0} symbols remain to finish historical.");
                collection.Add( LogMessage.LOGMSG1067,"StartRealTime()");
                collection.Add( LogMessage.LOGMSG1068,"Final check point completed. Resuming Engine task.");
                collection.Add( LogMessage.LOGMSG1069,"ControllerFinished() count = {0}");
                collection.Add( LogMessage.LOGMSG1070,"Removed last controller. State change to {0}");
                collection.Add( LogMessage.LOGMSG1071,"CancellationPending. Stopping all controllers.");
                collection.Add( LogMessage.LOGMSG1072,"MaxCount({0})");
                collection.Add( LogMessage.LOGMSG1073,"Stopping the engine task");
                collection.Add( LogMessage.LOGMSG1074,"Disposing of the engine context");
                collection.Add( LogMessage.LOGMSG1075,"Changed isFinalized to {0}");
                collection.Add( LogMessage.LOGMSG1076,"CurrentRunMode changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG1077,"State changed from {0} to {1}");
                collection.Add( LogMessage.LOGMSG1078,"PositionChanged");
                collection.Add( LogMessage.LOGMSG1079,"new SymbolController({0})");
                collection.Add( LogMessage.LOGMSG1080,"Update chart is {0}");
                collection.Add( LogMessage.LOGMSG1081,"InitializeTick({0})");
                collection.Add( LogMessage.LOGMSG1082,"Selecting strategy or portfolio to control provider...");
                collection.Add( LogMessage.LOGMSG1083,"Skipping multi-symbol portfolio: {0}");
                collection.Add( LogMessage.LOGMSG1084,"Skipping {0} which doesn't implement StrategyInterface");
                collection.Add( LogMessage.LOGMSG1085,"ActiveChange: {0} is {1}: Now {2} active.");
                collection.Add( LogMessage.LOGMSG1086,"StartTickLoop()");
                collection.Add( LogMessage.LOGMSG1087,"ResumeAfterCheckPoint()");
                collection.Add( LogMessage.LOGMSG1088,"Resumed until next check point at state {0}");
                collection.Add( LogMessage.LOGMSG1089,"Pausing controller loop at checkpoint {0} before tick {1}");
                collection.Add( LogMessage.LOGMSG1090,"Queue returned {0}");
                collection.Add( LogMessage.LOGMSG1091,"StartHistorical()");
                collection.Add( LogMessage.LOGMSG1092,"EndHistorical()");
                collection.Add( LogMessage.LOGMSG1093,"Removing controller {0}");
                collection.Add( LogMessage.LOGMSG1094,"Send EndHistorical events for controller {0}");
                collection.Add( LogMessage.LOGMSG1095,"Final processing on controller for symbol {0}");
                collection.Add( LogMessage.LOGMSG1096,"Verifying all data intervals");
                collection.Add( LogMessage.LOGMSG1097,"Updating the chart.");
                collection.Add( LogMessage.LOGMSG1098,"TryShowCharts() symbolMode = {0}, realTimeOutput = {1}, tickCount = {2}");
                collection.Add( LogMessage.LOGMSG1099,"EndRealTime()");
                collection.Add( LogMessage.LOGMSG1100,"Responding to RequestPosition as 0. No signal driver set yet.");
                collection.Add( LogMessage.LOGMSG1101,"Can't switch to broker. Signal driver model is null.");
                collection.Add( LogMessage.LOGMSG1102,"RequestPosition() sending position change, position {0}");
                collection.Add( LogMessage.LOGMSG1103,"StartBroker() changing to connected.");
                collection.Add( LogMessage.LOGMSG1104,"StartBroker() already connected.");
                collection.Add( LogMessage.LOGMSG1105,"EndBroker() changing to disconnected - {0}");
                collection.Add( LogMessage.LOGMSG1106,"EndBroker() already disconnected.");
                collection.Add( LogMessage.LOGMSG1107,"isWaitingForCheckpoint - Pause()");
                collection.Add( LogMessage.LOGMSG1108,"Inserting market replay delay of {0}ms.");
                collection.Add( LogMessage.LOGMSG1109,"not active - NoWork");
                collection.Add( LogMessage.LOGMSG1110,"Resuming controller loop at checkpoint {0}.");
                collection.Add( LogMessage.LOGMSG1111,"chartRenderLock - NoWork");
                collection.Add( LogMessage.LOGMSG1112,"ProcessTick( {0} )");
                collection.Add( LogMessage.LOGMSG1113,"chartRenderLock2 - NoWork");
                collection.Add( LogMessage.LOGMSG1114,"EndCacheData() desired run mode {0}, current run mode {1}, symbol mode {2}");
                collection.Add( LogMessage.LOGMSG1115,"SetupRealTime");
                collection.Add( LogMessage.LOGMSG1116,"Skipped {0} beginning ticks.");
                collection.Add( LogMessage.LOGMSG1117,"Processing CheckPointTick {0}");
                collection.Add( LogMessage.LOGMSG1118,"ProcessTouchEvent( {0} ) ");
                collection.Add( LogMessage.LOGMSG1119,"Order for id {0} was not found. Ignoring.");
                collection.Add( LogMessage.LOGMSG1120,"Fill matches logical order {0}");
                collection.Add( LogMessage.LOGMSG1121,"Order for touch already canceled: {0}");
                collection.Add( LogMessage.LOGMSG1122,"Symbol recency now {0}");
                collection.Add( LogMessage.LOGMSG1123,"Touch recency {0} less than or equal to symbol recency {1}.");
                collection.Add( LogMessage.LOGMSG1124,"Enqueuing logical fill: {0}");
                collection.Add( LogMessage.LOGMSG1125,"ProcessFillEvent( {0} ) ");
                collection.Add( LogMessage.LOGMSG1126,"Fill recency {0} less than or equal to symbol recency {1}. Probably an exit strategy order.");
                collection.Add( LogMessage.LOGMSG1127,"Order for fill already canceled. Provider will offset the position.");
                collection.Add( LogMessage.LOGMSG1128,"Live fill received while broker is disconnected. Ignoring to avoid duplicated simulated fills.");
                collection.Add( LogMessage.LOGMSG1129,"order {0}, strategy {1}, fill {2}, change {3}, new position {4}");
                collection.Add( LogMessage.LOGMSG1130,"Marking order id {0} filled.");
                collection.Add( LogMessage.LOGMSG1131,"Canceling Enter order after partial {0} fill: {1}");
                collection.Add( LogMessage.LOGMSG1132,"TrySwitchToBroker()");
                collection.Add( LogMessage.LOGMSG1133,"Can't switch to broker. No signal driver set yet.");
                collection.Add( LogMessage.LOGMSG1134,"TrySyncSimulate()");
                collection.Add( LogMessage.LOGMSG1135,"TryNewSendAndFillOrders() isTimeSync {0}, syncTicksEnabled {1}");
                collection.Add( LogMessage.LOGMSG1136,"TryNewSendAndFillOrders() IsProcessingOrders {0}, SentOrderChange {1}");
                collection.Add( LogMessage.LOGMSG1137,"TryNewSendAndFillOrders() position changed need {0}");
                collection.Add( LogMessage.LOGMSG1138,"TryNewSendAndFillOrders() position {0}");
                collection.Add( LogMessage.LOGMSG1139,"TryNewSendAndFillOrders() sending position change, position {0}");
                collection.Add( LogMessage.LOGMSG1140,"DataFeed={0}, Broker={1}, isForced={2}, ordersChanged={3}, positionChanged={4}");
                collection.Add( LogMessage.LOGMSG1141,"Sending PositionChanged {0}");
                collection.Add( LogMessage.LOGMSG1142,"Strategy Position {0}");
                collection.Add( LogMessage.LOGMSG1143,"Incrementing position change loop counter to {0}");
                collection.Add( LogMessage.LOGMSG1144,"EndStrategyPeriods event");
                collection.Add( LogMessage.LOGMSG1145,"{0} bar close at {1}");
                collection.Add( LogMessage.LOGMSG1146,"SynchronizePortfolio event");
                collection.Add( LogMessage.LOGMSG1147,"EndFinalStrategyPeriods event");
                collection.Add( LogMessage.LOGMSG1148,"Update Chart: Called chart.AddBar()");
                collection.Add( LogMessage.LOGMSG1149,"Update Chart: Called chart.Update()");
                collection.Add( LogMessage.LOGMSG1150,"NewStrategyPeriods event");
                collection.Add( LogMessage.LOGMSG1151,"InitPeriod()");
                collection.Add( LogMessage.LOGMSG1152,"InitPeriod series:{0}");
                collection.Add( LogMessage.LOGMSG1153,"EngineIntervalOpen for model:{0}");
                collection.Add( LogMessage.LOGMSG1154,"Disposing exit strategy simulator.");
                collection.Add( LogMessage.LOGMSG1155,"Disposing provider simulator.");
                collection.Add( LogMessage.LOGMSG1156,"Disposing controller receiver.");
                collection.Add( LogMessage.LOGMSG1157,"Stopping controller task.");
                collection.Add( LogMessage.LOGMSG1158,"PeriodFlags");
                collection.Add( LogMessage.LOGMSG1159,"flags:   {0} ( Chart: {1})");
                collection.Add( LogMessage.LOGMSG1160,"SymbolReceiver()");
                collection.Add( LogMessage.LOGMSG1161,"Sent StartSymbol()");
                collection.Add( LogMessage.LOGMSG1162,"Received {0}");
                collection.Add( LogMessage.LOGMSG1163,"Terminating because queue returned {0}");
                collection.Add( LogMessage.LOGMSG1164,"Stopping task.");
                collection.Add( LogMessage.LOGMSG1165,"ProcessTick({0})");
                collection.Add( LogMessage.LOGMSG1166,"{0} matched trigger: {1}");
                collection.Add( LogMessage.LOGMSG1167,"Adding Bar at tick={0}");
                collection.Add( LogMessage.LOGMSG1168,"clear {0}()");
                collection.Add( LogMessage.LOGMSG1169,"{0}.InitializeTick({1})");
                collection.Add( LogMessage.LOGMSG1170,"Found the following bar IntervalsInternal...");
                collection.Add( LogMessage.LOGMSG1171,"   {0}");
                collection.Add( LogMessage.LOGMSG1172,"{0}.AddInterval({1})");
                collection.Add( LogMessage.LOGMSG1173,"GetStartTime( {0}, {1} next start time {2}");
                collection.Add( LogMessage.LOGMSG1174,"CheckForSessionStart( {0}, next start time {1}, next session start {2}");
                collection.Add( LogMessage.LOGMSG1175,"Unable to set timer. Time to fire was {0}ms which was less than minimum delay of {1}");
                collection.Add( LogMessage.LOGMSG1176,"IntervalTimer for {0}");
                collection.Add( LogMessage.LOGMSG1177,"Adding new bar from tick: {0}");
                collection.Add( LogMessage.LOGMSG1178,"NeedsNewBar( {0}, {1} >= {2}) is true.");
                collection.Add( LogMessage.LOGMSG1179,"NeedsNewBar( {0}, {1} >= {2}) is false.");
                collection.Add( LogMessage.LOGMSG1180, "Chain: {0}");
		        collection.Add( LogMessage.LOGMSG1181, "gain.Add({0})");
		        collection.Add( LogMessage.LOGMSG1182, "loss.Add(0)");
                collection.Add( LogMessage.LOGMSG1183, "gain.Add(0)");
                collection.Add( LogMessage.LOGMSG1184, "loss.Add({0})");
		        collection.Add(LogMessage.LOGMSG1185, "client id {0} for {1}");
		}

        public void ConfigureUserLog()
        {
            this.repositoryName = "Log";
            lock (repositoryLocker)
            {
                this.repository = LoggerManager.CreateRepository(repositoryName);
            }
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

        public void RealTimeForSymbol(string symbol)
        {
            var hierarchy = (Hierarchy)repository;
            if (hierarchy.Root.Level == null || hierarchy.Root.Level > Level.Debug)
            {
                hierarchy.Root.Level = Level.Debug;
                foreach (var kvp in map)
                {
                    var logger = kvp.Value;
                    logger.NofityLogLevelChange();
                }
            }
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
            lock (repositoryLocker)
            {
                if (repositoryName == "SysLog" && ConfigurationManager.GetSection("log4net") != null)
                {
                    log4net.Config.XmlConfigurator.Configure(repository);
                }
                else
                {
                    var xml = GetConfigXML(repositoryName, extension, defaultConfig);
                    repository.ResetConfiguration();
                    log4net.Config.XmlConfigurator.Configure(repository, xml);
                }
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
            lock (repositoryLocker)
            {
                foreach (var appender in repository.GetAppenders())
                {
                    var buffer = appender as BufferingAppenderSkeleton;
                    if (buffer != null)
                    {
                        buffer.Flush();
                    }
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
                lock (repositoryLocker)
                {
                    ILogger logger = repository.GetLogger(type.FullName);
			        log = new LogImpl(this,logger);
                }
                map[type.FullName] = log;
			}
			return log;
		}
		public Log GetLogger(string name) {
			LogImpl log;
			if( map.TryGetValue(name, out log)) {
				return log;
			} else {
                lock (repositoryLocker)
                {
                    ILogger logger = repository.GetLogger(name);
                    log = new LogImpl(this, logger);
                }
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
    <appender name=""FixLogAppender"" type=""TickZoom.Logging.RollingFileAppender"">
      <rollingStyle value=""Size"" />
      <maxSizeRollBackups value=""10"" />
      <maximumFileSize value=""100MB"" />
      <file value=""LogFolder\FIX.log"" />
      <appendToFile value=""false"" />
      <layout type=""log4net.Layout.PatternLayout"">
        <conversionPattern value=""%logger - %message%newline"" />
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
