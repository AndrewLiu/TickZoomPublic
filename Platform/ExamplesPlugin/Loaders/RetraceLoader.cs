using System.Collections.Generic;
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    public class RetraceLoader : ModelLoaderCommon
    {
        public RetraceLoader()
        {
            /// <summary>
            /// IMPORTANT: You can personalize the name of each model loader.
            /// </summary>
            category = "Example";
            name = "Retrace Multi-Symbol";
        }

        public override void OnInitialize(ProjectProperties properties)
        {
        }

        public override void OnLoad(ProjectProperties properties)
        {
            var strategies = new List<Strategy>();
            foreach (var symbol in properties.Starter.SymbolInfo)
            {
                symbol.SimulateSpread = true;
                CreateRetraceStrategy(symbol, strategies, RetraceDirection.LongOnly);
                //CreateRetraceStrategy(symbol, strategies, RetraceDirection.ShortOnly);
            }

            var portfolio = new Portfolio();
            portfolio.Performance.Equity.GraphEquity = true;
            foreach (var strategy in strategies)
            {
                portfolio.AddDependency(strategy);
            }
            TopModel = portfolio;
         }

        private void CreateRetraceStrategy(SymbolInfo symbol, List<Strategy> strategies, RetraceDirection direction)
        {
            var strategy = new Retrace2Strategy();
            strategy.Direction = direction;
            strategy.SymbolDefault = symbol.ExpandedSymbol;
            strategy.IsActive = true;
            strategy.IsVisible = false;
            strategies.Add(strategy);
        }
    }
}