using System;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public class SyntheticOrderRouter : PhysicalOrderHandler
    {
        private Agent receiver;
        private Agent self;
        private SymbolInfo symbol;
        public SyntheticOrderRouter( SymbolInfo symbol, Agent self, Agent receiver)
        {
            this.receiver = receiver;
            this.self = self;
            this.symbol = symbol;
        }

        public bool OnChangeBrokerOrder(CreateOrChangeOrder order)
        {
            if( receiver == null) return false;
            receiver.SendEvent(new EventItem(self, symbol.CommonSymbol, EventType.SyntheticOrder, order));
            return true;
        }

        public bool OnCreateBrokerOrder(CreateOrChangeOrder order)
        {
            if (receiver == null) return false;
            receiver.SendEvent(new EventItem(self, symbol.CommonSymbol, EventType.SyntheticOrder, order));
            return true;
        }

        public bool OnCancelBrokerOrder(CreateOrChangeOrder order)
        {
            if (receiver == null) return false;
            receiver.SendEvent(new EventItem(self, symbol.CommonSymbol, EventType.SyntheticOrder, order));
            return true;
        }

        public int ProcessOrders()
        {
            return 0;
        }

        public void Clear()
        {
            if (receiver == null) return;
            receiver.SendEvent(new EventItem(self, symbol.CommonSymbol, EventType.SyntheticClear));
        }

        public bool IsChanged
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

    }
}