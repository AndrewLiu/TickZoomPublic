using System;

namespace TickZoom.Api
{
    public class StrategyPositionDefault : StrategyPosition
    {
        private static readonly Log log = Factory.SysLog.GetLogger(typeof (StrategyPositionDefault));
        private static readonly bool debug = log.IsDebugEnabled;
        private static readonly bool trace = log.IsTraceEnabled;
        private int _id;
        private SymbolInfo _symbol;
        private long position;

        public StrategyPositionDefault()
        {
            
        }

        public void Initialize(int id, SymbolInfo symbol)
        {
            this._id = id;
            this._symbol = symbol;
            if( trace) log.TraceFormat(LogMessage.LOGMSG689);
        }

        public long ExpectedPosition
        {
            get { return this.position; }
        }

        public SymbolInfo Symbol
        {
            get { return _symbol; }
        }

        public int Id
        {
            get { return _id; }
        }

        public void SetExpectedPosition(long position)
        {
            if (trace) log.TraceFormat(LogMessage.LOGMSG690, Id, Symbol, this.position, position);
            this.position = position;
        }

        public void TrySetPosition( long position)
        {
            if (position != this.position)
            {
                if (debug) log.DebugFormat(LogMessage.LOGMSG691, _id, _symbol, this.position, position);
                this.position = position;
            }
            else
            {
                if (trace) log.TraceFormat(LogMessage.LOGMSG692, _id, _symbol, this.position);
            }
        }

        public override string ToString()
        {
            return "Strategy " + Id + ", " + _symbol + ", position " + position ;
        }
    }
}