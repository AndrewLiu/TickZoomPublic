using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TickZoom.Api;
using TickZoom.Common;
using TickZoom.Examples.Strategies;

namespace TickZoom.Examples
{
    public class ClientSimulatorLoader : ModelLoaderCommon
    {
        public ClientSimulatorLoader()
        {
            
            category = "Test";
            name = "Client Simulator";
        }

        public override void OnInitialize(ProjectProperties properties)
        {
        }

        public override void OnLoad(ProjectProperties properties)
        {
            var portfolio = new Portfolio();
            foreach (ISymbolProperties symbol in properties.Starter.SymbolProperties)
            {
                if( symbol.Account != "market")
                {
                    string name = symbol.ExpandedSymbol;
                    Strategy strategy = new ClientSimulatorStrategy();
                    strategy.SymbolDefault = name;
                    strategy.Performance.Equity.GraphEquity = false;
                    portfolio.AddDependency(strategy);
                }
            }

            TopModel = portfolio;
        }
    }
}
