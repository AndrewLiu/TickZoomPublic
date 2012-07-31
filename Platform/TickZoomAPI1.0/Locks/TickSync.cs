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
using System.Runtime.InteropServices;
using System.Threading;

namespace TickZoom.Api
{
    [SerializeContract]
    [StructLayout(LayoutKind.Sequential)]
    unsafe public struct TickSyncState
    {
        [SerializeMember(1)]
        public int isLocked;
        [SerializeMember(2)]
        public long symbolBinaryId;
        [SerializeMember(3)]
        public int ticks;
        [SerializeMember(4)]
        public int positionChange;
        [SerializeMember(5)]
        public int waitingMatch;
        [SerializeMember(6)]
        public int processPhysical;
        [SerializeMember(7)]
        public int reprocessPhysical;
        [SerializeMember(8)]
        public int physicalFillsCreated;
        [SerializeMember(9)]
        public int physicalFillsWaiting;
        [SerializeMember(10)]
        public int physicalOrders;
        [SerializeMember(11)]
        public int orderChange;
        [SerializeMember(12)]
        public int switchBrokerState;
        public override string ToString()
        {
            return "TickSync Ticks ( " + ticks + ", Locked " + isLocked + " )" +
                ", Orders ( Sent " + physicalOrders + ", Changed " + orderChange +
                    ", Process " + processPhysical + ", Reprocess " + reprocessPhysical + " )" +
                ", Fills ( Created " + physicalFillsCreated + ", Waiting " + physicalFillsWaiting + " )" +
                ", Position Changes ( Sent " + positionChange + ", Waiting " + waitingMatch + " )" +
                ", Switch Broker " + switchBrokerState;
        }
    }

    public unsafe class TickSync
    {
        private static readonly Log staticLog = Factory.SysLog.GetLogger(typeof(TickSync));
        private readonly bool debug = staticLog.IsDebugEnabled;
        private readonly bool trace = staticLog.IsTraceEnabled;
        private Log log;
        private TickSyncState* state;
        private SymbolInfo symbolInfo;
        private string symbol;
        private TimeStamp lastAddTime = TimeStamp.UtcNow;
        private Action changeCallBack;

        internal TickSync(long symbolId, TickSyncState* tickSyncPtr)
        {
            state = tickSyncPtr;
            (*state).symbolBinaryId = symbolId;
            symbolInfo = Factory.Symbol.LookupSymbol(symbolId);
            symbol = symbolInfo.ExpandedSymbol.StripInvalidPathChars();
            log = Factory.SysLog.GetLogger(typeof(TickSync).FullName + "." + symbol);
            if (trace) log.TraceFormat(LogMessage.LOGMSG657, symbolId);
        }

        public bool Completed
        {
            get
            {
                var value = CheckCompletedInternal();
                return value;
            }
        }

        public bool CompletedExceptPositionChange
        {
            get { return CheckCompletedExceptPositionChange(); }
            
        }
        private bool CheckCompletedExceptPositionChange()
        {
            return (*state).ticks == 0 && (*state).switchBrokerState == 0 &&
                   (*state).waitingMatch == 0 && (*state).orderChange == 0 &&
                   (*state).physicalOrders == 0 && (*state).physicalFillsCreated == 0 &&
                   (*state).processPhysical == 0 && (*state).reprocessPhysical == 0;
        }
        private bool CheckCompletedInternal()
        {
            return (*state).ticks == 0 && (*state).positionChange == 0 && (*state).switchBrokerState == 0 &&
                   (*state).waitingMatch == 0 && (*state).orderChange == 0 &&
                   (*state).physicalOrders == 0 && (*state).physicalFillsCreated == 0 &&
                   (*state).processPhysical == 0 && (*state).reprocessPhysical == 0;
        }
        private bool CheckOnlyProcessingOrders()
        {
            return (*state).physicalOrders == 0 && (*state).positionChange == 0 && (*state).physicalFillsCreated == 0 && (*state).processPhysical > 0;
        }

        private bool CheckOnlyReprocessOrders()
        {
            return (*state).physicalOrders == 0 && (*state).positionChange == 0 && (*state).physicalFillsCreated == 0 && (*state).reprocessPhysical > 0;
        }

        private void Changed()
        {
            if( changeCallBack != null)
            {
                changeCallBack();
            }
        }

        private bool CheckProcessingOrders()
        {
            return (*state).positionChange > 0 || (*state).waitingMatch > 0 || (*state).physicalOrders > 0 || 
                    (*state).switchBrokerState > 0 || (*state).physicalFillsCreated > 0 || (*state).processPhysical > 0 ||
                   (*state).reprocessPhysical > 0;
        }

        public void Clear()
        {
            if (!CheckCompletedInternal())
            {
                log.Error("All counters must complete to 0 before clearing the tick sync. Currently: " + (*state));
                //System.Diagnostics.Debugger.Break();
                //throw new ApplicationException("Tick, position changes, physical orders, and physical fills, must all complete before clearing the tick sync. Current numbers are: " + state);
            }
            ForceClear("Clear()");
        }

        public bool TryLock()
        {
            return Interlocked.CompareExchange(ref (*state).isLocked, 1, 0) == 0;
        }

        public bool IsLocked
        {
            get { return (*state).isLocked == 1; }
        }

        public void Unlock()
        {
            Interlocked.Exchange(ref (*state).isLocked, 0);
        }

        public void ForceClear(string message)
        {
            Interlocked.Exchange(ref (*state).ticks, 0);
            Interlocked.Exchange(ref (*state).physicalOrders, 0);
            Interlocked.Exchange(ref (*state).orderChange, 0);
            Interlocked.Exchange(ref (*state).processPhysical, 0);
            Interlocked.Exchange(ref (*state).reprocessPhysical, 0);
            Interlocked.Exchange(ref (*state).positionChange, 0);
            Interlocked.Exchange(ref (*state).waitingMatch, 0);
            Interlocked.Exchange(ref (*state).switchBrokerState, 0);
            Interlocked.Exchange(ref (*state).physicalFillsCreated, 0);
            Interlocked.Exchange(ref (*state).physicalFillsWaiting, 0);
            Unlock();
            if (trace) log.TraceFormat(LogMessage.LOGMSG658, message, *state);
        }

        public override string ToString()
        {
            return (*state).ToString();
        }

        public TickSyncState State
        {
            get { return *state; }
        }

        public void AddTick(Tick tick)
        {
            lastAddTime = Factory.Parallel.UtcNow; 
            var value = Interlocked.Increment(ref (*state).ticks);
            if (trace) log.TraceFormat(LogMessage.LOGMSG659, tick.Extract(), *state);
            if( value > 1)
            {
                throw new ApplicationException("Tick counter was allowed to go over 1.");
            }
        }

        public void RemoveTick(ref TickBinary tick)
        {
            var value = Interlocked.Decrement(ref (*state).ticks);
            if (trace)
            {
                var callback = changeCallBack == null ? "" : " Callback, ";
                log.TraceFormat(LogMessage.LOGMSG660, callback + value, tick, *state);
            }
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).ticks);
                if (debug) log.DebugFormat(LogMessage.LOGMSG661, value, temp);
            }
            Changed();
        }

        public void AddPhysicalFill(object fill)
        {
            lastAddTime = TimeStamp.UtcNow; 
            var valueCreated = Interlocked.Increment(ref (*state).physicalFillsCreated);
            var valueWaiting = Interlocked.Increment(ref (*state).physicalFillsWaiting);
            if (trace) log.TraceFormat(LogMessage.LOGMSG662, valueCreated, valueWaiting, fill, *state);
        }

        public void RemovePhysicalFill(object fill)
        {
            var valueCreated = Interlocked.Decrement(ref (*state).physicalFillsCreated);
            var valueWaiting = Interlocked.Decrement(ref (*state).physicalFillsWaiting);
            if (trace) log.TraceFormat(LogMessage.LOGMSG663, valueCreated, valueWaiting, fill, *state);
            if (valueCreated < 0)
            {
                var temp = Interlocked.Increment(ref (*state).physicalFillsCreated);
                if (debug) log.DebugFormat(LogMessage.LOGMSG664, valueCreated, temp);
            }
            if (valueWaiting < 0)
            {
                var temp = Interlocked.Increment(ref (*state).physicalFillsWaiting);
                if (debug) log.DebugFormat(LogMessage.LOGMSG665, valueWaiting, temp);
            }
            Changed();
        }

        public void RemovePhysicalFillWaiting(object fill)
        {
            var valueWaiting = Interlocked.Decrement(ref (*state).physicalFillsWaiting);
            if (trace) log.TraceFormat(LogMessage.LOGMSG666, valueWaiting, fill, *state);
            if (valueWaiting < 0)
            {
                var temp = Interlocked.Increment(ref (*state).physicalFillsWaiting);
                if (debug) log.DebugFormat(LogMessage.LOGMSG665, valueWaiting, temp);
            }
            Changed();
        }

        public void AddOrderChange()
        {
            lastAddTime = TimeStamp.UtcNow;
            var value = Interlocked.Increment(ref (*state).orderChange);
            if (trace) log.TraceFormat(LogMessage.LOGMSG667, value, *state);
        }

        public void RemoveOrderChange()
        {
            var value = Interlocked.Decrement(ref (*state).orderChange);
            if (trace) log.TraceFormat(LogMessage.LOGMSG668, value, *state);
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).orderChange);
                if (debug) log.DebugFormat(LogMessage.LOGMSG669, value, temp);
            }
            Changed();
        }

        public void AddPhysicalOrder(object order)
        {
            lastAddTime = TimeStamp.UtcNow; 
            var value = Interlocked.Increment(ref (*state).physicalOrders);
            if (trace) log.TraceFormat(LogMessage.LOGMSG670, value, order, *state);
        }

        public void RemovePhysicalOrder(object order)
        {
            var value = Interlocked.Decrement(ref (*state).physicalOrders);
            if (trace) log.TraceFormat(LogMessage.LOGMSG671, value, order, *state);
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).physicalOrders);
                if (debug) log.DebugFormat(LogMessage.LOGMSG672, value, temp);
            }
            Changed();
        }

        public void RemovePhysicalOrder()
        {
            var value = Interlocked.Decrement(ref (*state).physicalOrders);
            if (trace) log.TraceFormat(LogMessage.LOGMSG673, value, *state);
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).physicalOrders);
                if (debug) log.DebugFormat(LogMessage.LOGMSG672, value, temp);
            }
            Changed();
        }

        public void SetSwitchBrokerState(string description)
        {
            lastAddTime = TimeStamp.UtcNow;
            if ((*state).switchBrokerState == 0)
            {
                var value = Interlocked.Increment(ref (*state).switchBrokerState);
                if (trace) log.TraceFormat(LogMessage.LOGMSG674, description, value, *state);
            }
        }

        public void ClearSwitchBrokerState(string description)
        {
            var value = Interlocked.Decrement(ref (*state).switchBrokerState);
            if (trace) log.TraceFormat(LogMessage.LOGMSG675, description, value, *state);
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).switchBrokerState);
                if (debug) log.DebugFormat(LogMessage.LOGMSG676, value, temp);
            }
            Changed();
        }

        public void AddPositionChange(string description)
        {
            lastAddTime = TimeStamp.UtcNow; 
            var value = Interlocked.Increment(ref (*state).positionChange);
            if (trace) log.TraceFormat(LogMessage.LOGMSG677, description, value, *state);
        }

        public void RemovePositionChange(string description)
        {
            var value = Interlocked.Decrement(ref (*state).positionChange);
            if (trace) log.TraceFormat(LogMessage.LOGMSG678, description, value, *state);
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).positionChange);
                if (debug) log.DebugFormat(LogMessage.LOGMSG679, value, temp);
            }
            Changed();
        }

        public void AddWaitingMatch(string description)
        {
            lastAddTime = TimeStamp.UtcNow;
            var value = Interlocked.Increment(ref (*state).waitingMatch);
            if (trace) log.TraceFormat(LogMessage.LOGMSG680, description, value, *state);
        }

        public void RemoveWaitingMatch(string description)
        {
            var value = Interlocked.Decrement(ref (*state).waitingMatch);
            if (trace) log.TraceFormat(LogMessage.LOGMSG681, description, value, *state);
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).waitingMatch);
                if (debug) log.DebugFormat(LogMessage.LOGMSG682, value, temp);
            }
            Changed();
        }

        public void AddProcessPhysicalOrders()
        {
            lastAddTime = Factory.Parallel.UtcNow; 
            var value = Interlocked.Increment(ref (*state).processPhysical);
            if (trace) log.TraceFormat(LogMessage.LOGMSG683, value, *state);
            Changed();
        }

        public void RemoveProcessPhysicalOrders()
        {
            var value = Interlocked.Decrement(ref (*state).processPhysical);
            if (trace) log.TraceFormat(LogMessage.LOGMSG684, value, *state);
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).processPhysical);
                if( debug) log.DebugFormat(LogMessage.LOGMSG685, value, temp);
            }
            Changed();
        }

        public void SetReprocessPhysicalOrders()
        {
            lastAddTime = Factory.Parallel.UtcNow; 
            if ((*state).reprocessPhysical == 0)
            {
                var value = Interlocked.Increment(ref (*state).reprocessPhysical);
                if (trace) log.TraceFormat(LogMessage.LOGMSG686, value, *state);
            }
            Changed();
        }

        public void ClearReprocessPhysicalOrders()
        {
            var value = Interlocked.Decrement(ref (*state).reprocessPhysical);
            if (trace) log.TraceFormat(LogMessage.LOGMSG687, value, *state);
            if (value < 0)
            {
                var temp = Interlocked.Increment(ref (*state).reprocessPhysical);
                if (debug) log.DebugFormat(LogMessage.LOGMSG688, value, temp);
            }
            Changed();
        }

        public long SymbolBinaryId
        {
            get { return (*state).symbolBinaryId; }
        }

        public bool SentPhysicalFillsWaiting
        {
            get { return (*state).physicalFillsWaiting > 0; }
        }

        public bool SentPhysicalFillsCreated
        {
            get { return (*state).physicalFillsCreated > 0; }
        }

        public bool SentPositionChange
        {
            get { return (*state).positionChange > 0; }
        }

        public bool IsWaitingMatch
        {
            get { return (*state).positionChange == 0 && (*state).waitingMatch > 0; }
        }

        public bool SentWaitingMatch
        {
            get { return (*state).waitingMatch > 0; }
        }

        public bool SentSwitchBrokerState
        {
            get { return (*state).switchBrokerState > 0; }
        }

        public bool OnlyReprocessPhysicalOrders
        {
            get { return CheckOnlyReprocessOrders(); }
        }

        public bool OnlyProcessPhysicalOrders
        {
            get { return CheckOnlyProcessingOrders(); }
        }

        public bool IsProcessingOrders
        {
            get { return CheckProcessingOrders(); }
        }

        public bool SentProcessPhysicalOrders
        {
            get { return (*state).processPhysical > 0; }
        }

        public bool SentReprocessPhysicalOrders
        {
            get { return (*state).reprocessPhysical > 0; }
        }

        public bool SentOrderChange
        {
            get { return (*state).orderChange > 0; }
        }

        public bool SentPhyscialOrders
        {
            get { return (*state).physicalOrders > 0; }
        }

        public Action ChangeCallBack
        {
            get { return changeCallBack; }
            set { changeCallBack = value; }
        }
    }
}
