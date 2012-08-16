using System.Collections.Generic;
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    public class PivotLoader : ModelLoaderCommon
    {
        public PivotLoader()
        {
            /// <summary>
            /// IMPORTANT: You can personalize the name of each model loader.
            /// </summary>
            category = "Example";
            name = "Pivot Trend Multi-Symbol";
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
                CreateStrategy(symbol, strategies);
            }

            var portfolio = new Portfolio();
            portfolio.Performance.Equity.GraphEquity = true;
            foreach (var strategy in strategies)
            {
                portfolio.AddDependency(strategy);
            }
            TopModel = portfolio;
        }

        private void CreateStrategy(SymbolInfo symbol, List<Strategy> strategies)
        {
            var strategy = new PivotTrend();
            strategy.SymbolDefault = symbol.ExpandedSymbol;
            strategy.IsActive = true;
            //strategy.IsVisible = false;
            strategies.Add(strategy);
        }
    }
}