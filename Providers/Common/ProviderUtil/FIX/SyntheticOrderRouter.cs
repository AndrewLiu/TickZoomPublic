using System;
using TickZoom.Api;

namespace TickZoom.FIX
{
    public class SyntheticOrderRouter : PhysicalOrderHandler
    {
        private Agent receiver;
        private Agent self;
        public SyntheticOrderRouter( Agent self, Agent receiver)
        {
            this.receiver = receiver;
            this.self = self;
        }
        public bool OnChangeBrokerOrder(CreateOrChangeOrder order)
        {
            return receiver.SendEvent(new EventItem(self, EventType.SyntheticOrder, order));
        }

        public bool OnCreateBrokerOrder(CreateOrChangeOrder order)
        {
            return receiver.SendEvent(new EventItem(self, EventType.SyntheticOrder, order));
        }

        public bool OnCancelBrokerOrder(CreateOrChangeOrder order)
        {
            return receiver.SendEvent(new EventItem(self, EventType.SyntheticOrder, order));
        }

        public int ProcessOrders()
        {
            return 0;
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