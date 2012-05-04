using System;
using System.Collections.Generic;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public class SymbolReceivers
    {
        private readonly object symbolRequestsLocker = new object();
        private Dictionary<long, SymbolReceiver> symbolRequests = new Dictionary<long, SymbolReceiver>();

        private SymbolInfo GetSource( SymbolInfo symbol)
        {
            return symbol.SourceSymbol;
        }

        public SymbolReceiver[] GetReceivers()
        {
            var list = new List<SymbolReceiver>();
            lock (symbolRequestsLocker)
            {
                foreach( var kvp in symbolRequests)
                {
                    list.Add(kvp.Value);
                }
            }
            return list.ToArray();
        }
        public SymbolReceiver GetSymbolRequest(SymbolInfo symbol)
        {
            lock (symbolRequestsLocker)
            {
                SymbolReceiver symbolReceiver;
                if (!symbolRequests.TryGetValue(GetSource(symbol).BinaryIdentifier, out symbolReceiver))
                {
                    throw new InvalidOperationException("Can't find symbol request for " + symbol);
                }
                return symbolReceiver;
            }
        }

        public bool GetSymbolStatus(SymbolInfo symbol)
        {
            lock (symbolRequestsLocker)
            {
                return symbolRequests.ContainsKey(GetSource(symbol).BinaryIdentifier);
            }
        }

        public bool TryAddSymbol(SymbolInfo symbol, Agent agent)
        {
            lock (symbolRequestsLocker)
            {
                if (!symbolRequests.ContainsKey(GetSource(symbol).BinaryIdentifier))
                {
                    symbolRequests.Add(GetSource(symbol).BinaryIdentifier, new SymbolReceiver { Symbol = symbol, Agent = agent });
                    return true;
                }
            }
            return false;
        }

        public bool TryRemoveSymbol(SymbolInfo symbol)
        {
            lock (symbolRequestsLocker)
            {
                return symbolRequests.Remove(GetSource(symbol).BinaryIdentifier);
            }
        }

    }
}