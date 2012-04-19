using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    public class TwoSidedTestLoader : ModelLoaderCommon
    {
        private MarketSimulatorLoader marketSimulator;
        public TwoSidedTestLoader()
        {
            /// <summary>
            /// You can personalize the name of each model loader.
            /// </summary>
            category = "Test";
            name = "Two Sided Test";
            marketSimulator = new MarketSimulatorLoader();
        }

        public override void OnInitialize(ProjectProperties properties)
        {
            marketSimulator.OnInitialize(properties);
        }

        public override void OnLoad(ProjectProperties properties)
        {
            marketSimulator.OnLoad(properties);
            var portfolio = new Portfolio();
            portfolio.AddDependency(marketSimulator.TopModel);
            TopModel = portfolio;
        }
    }
}