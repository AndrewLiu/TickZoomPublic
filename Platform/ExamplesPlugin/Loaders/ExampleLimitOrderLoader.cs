﻿using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    public class ExampleLimitTruePartialLoader : ExampleLimitOrderLoader
    {
        public ExampleLimitTruePartialLoader()
        {
            category = "Example";
            name = "True Partial LimitOrders";
        }
        public override void OnInitialize(ProjectProperties properties) {
        }
		
        public override void OnLoad(ProjectProperties properties) {
            foreach (var symbol in properties.Starter.SymbolProperties)
            {
                symbol.PartialFillSimulation = PartialFillSimulation.PartialFillsIncomplete;
            }
            TopModel = GetStrategy("ExampleOrderStrategy");
        }
    }

    /// <summary>
    /// Description of Starter.
    /// </summary>
    public class ExampleLimitOrderLoader : ModelLoaderCommon
    {
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
            var portfolio = new Portfolio();
            foreach (ISymbolProperties symbol in properties.Starter.SymbolProperties)
            {
                var strategy = new ExampleOrderStrategy();
                strategy.SymbolDefault = symbol.ExpandedSymbol;
                portfolio.AddDependency(strategy);
            }

            TopModel = portfolio;
        }
    }
}