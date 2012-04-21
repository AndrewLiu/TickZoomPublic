using TickZoom.Api;
using TickZoom.Common;

namespace TickZoom.Examples
{
    public class TwoSidedTestLoader : ModelLoaderCommon
    {
        private MarketSimulatorLoader marketSimulator;
        private ClientSimulatorLoader clientSimulator;
        public TwoSidedTestLoader()
        {
            /// <summary>
            /// You can personalize the name of each model loader.
            /// </summary>
            category = "Test";
            name = "Two Sided Test";
            clientSimulator = new ClientSimulatorLoader();
            marketSimulator = new MarketSimulatorLoader();
        }

        public override void OnInitialize(ProjectProperties properties)
        {
            clientSimulator.OnInitialize(properties);
            marketSimulator.OnInitialize(properties);
        }

        public override void OnLoad(ProjectProperties properties)
        {
            clientSimulator.OnLoad(properties);
            marketSimulator.OnLoad(properties);
            var portfolio = new Portfolio();
            portfolio.AddDependency(clientSimulator.TopModel);
            portfolio.AddDependency(marketSimulator.TopModel);
            TopModel = portfolio;
        }
    }
}