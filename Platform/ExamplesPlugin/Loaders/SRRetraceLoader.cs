using System.Collections.Generic;
using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    /// <summary>
    /// Description of Starter.
    /// </summary>
    public class SRRetraceLoader : ModelLoaderCommon
    {
        public SRRetraceLoader() {
            /// <summary>
            /// IMPORTANT: You can personalize the name of each model loader.
            /// </summary>
            category = "S/R";
            name = "Retrace Example";
        }
		
        public override void OnInitialize(ProjectProperties properties) {
        }
		
        public override void OnLoad(ProjectProperties project) {
            var strategies = new List<Strategy>();
            foreach( var symbol in project.Starter.SymbolInfo) {
                var strategy = new SRRetrace();
                strategies.Add(strategy);
                strategy.SymbolDefault = symbol.ExpandedSymbol;
            }
			
            if( strategies.Count == 1) {
                TopModel = strategies[0];
            } else {
                var portfolio = new Portfolio();
                foreach( var strategy in strategies) {
                    portfolio.AddDependency(strategy);
                }
                TopModel = portfolio;
            }
        }
		
    }
}