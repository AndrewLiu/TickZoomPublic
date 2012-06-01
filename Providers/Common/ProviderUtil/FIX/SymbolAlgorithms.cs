using System;
using System.Collections.Generic;
using TickZoom.Api;

namespace TickZoom.Provider.FIX
{
    public class SymbolAlgorithms
    {
        private readonly object algorithmsLocker = new object();
        private Dictionary<long, SymbolAlgorithm> algorithms = new Dictionary<long, SymbolAlgorithm>();
        private FIXProviderSupport providerSupport;
        private bool disableChangeOrders;
        public SymbolAlgorithms(FIXProviderSupport providerSupport)
        {
            this.providerSupport = providerSupport;
        }

        public bool DisableChangeOrders
        {
            get { return disableChangeOrders; }
            set { disableChangeOrders = value; }
        }

        private SymbolInfo GetSource( SymbolInfo symbol)
        {
            return symbol.CommonSymbol;
        }

        public SymbolAlgorithm[] GetAlgorithms()
        {
            var list = new List<SymbolAlgorithm>();
            lock (algorithmsLocker)
            {
                foreach (var kvp in algorithms)
                {
                    list.Add(kvp.Value);
                }
                return list.ToArray();
            }
        }

        public SymbolAlgorithm CreateAlgorithm(SymbolInfo symbol)
        {
            SymbolAlgorithm symbolAlgorithm;
            lock (algorithmsLocker)
            {
                if (!algorithms.TryGetValue(GetSource(symbol).BinaryIdentifier, out symbolAlgorithm))
                {
                    var orderCache = Factory.Engine.LogicalOrderCache(symbol, false);
                    var syntheticRouter = new SyntheticOrderRouter(GetSource(symbol), providerSupport.Agent, providerSupport.Receiver);
                    var algorithm = Factory.Utility.OrderAlgorithm(providerSupport.ProviderName, symbol, providerSupport, syntheticRouter, orderCache, providerSupport.OrderStore);
                    algorithm.DisableChangeOrders = disableChangeOrders;
                    algorithm.EnableSyncTicks = SyncTicks.Enabled;
                    symbolAlgorithm = new SymbolAlgorithm { OrderAlgorithm = algorithm, Synthetics = syntheticRouter };
                    algorithms.Add(GetSource(symbol).BinaryIdentifier, symbolAlgorithm);
                    algorithm.OnProcessFill = providerSupport.ProcessFill;
                    algorithm.OnProcessTouch = providerSupport.ProcessTouch;
                }
            }
            return symbolAlgorithm;
        }

        public SymbolAlgorithm GetAlgorithm(SymbolInfo symbol)
        {
            SymbolAlgorithm symbolAlgorithm;
            lock (algorithmsLocker)
            {
                if (!algorithms.TryGetValue(GetSource(symbol).BinaryIdentifier, out symbolAlgorithm))
                {
                    throw new ApplicationException("OrderAlgorirhm was not found for " + symbol);
                }
            }
            return symbolAlgorithm;
        }

        public bool TryGetAlgorithm(SymbolInfo symbol, out SymbolAlgorithm algorithm)
        {
            lock (algorithmsLocker)
            {
                return algorithms.TryGetValue(GetSource(symbol).BinaryIdentifier, out algorithm);
            }
        }

        public void Reset()
        {
            lock (algorithmsLocker)
            {
                var symbols = new List<SymbolInfo>();
                foreach (var kvp in algorithms)
                {
                    var algo = kvp.Value.OrderAlgorithm;
                    symbols.Add(algo.Symbol);
                    algo.Clear();
                }
                algorithms.Clear();
                foreach (var symbol in symbols)
                {
                    CreateAlgorithm(symbol);
                }
            }
        }
    }
}