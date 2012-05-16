using System;
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    /// <summary>
    /// Description of Starter.
    /// </summary>
    public class ExampleLimitOrderLoader : ModelLoaderCommon
    {
        private double multiplier = 1.0;
        public ExampleLimitOrderLoader() {
            /// <summary>
            /// IMPORTANT: You can personalize the name of each model loader.
            /// </summary>
            category = "Example";
            name = "Limit Orders";
        }

        public override void OnInitialize(ProjectProperties properties) {
        }
		
        public override void OnLoad(ProjectProperties properties)
        {
            if( properties.Starter.SymbolInfo.Length > 1)
            {
                var portfolio = new Portfolio();
                portfolio.Name = "Portfolio-Client";
                foreach (ISymbolProperties symbol in properties.Starter.SymbolInfo)
                {
                    if( symbol.Account == "default")
                    {
                        var strategy = new ExampleOrderStrategy();
                        strategy.Multiplier = multiplier;
                        strategy.SymbolDefault = symbol.ExpandedSymbol;
                        portfolio.AddDependency(strategy);
                    }
                }

                TopModel = portfolio;
            }
            else
            {
                TopModel = new ExampleOrderStrategy();
            }
        }

        public double Multiplier
        {
            get { return multiplier; }
            set { multiplier = value; }
        }
    }
}