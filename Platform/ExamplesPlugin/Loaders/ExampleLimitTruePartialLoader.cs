using TickZoom.Api;

namespace TickZoom.Examples
{
    public class ExampleLimitTruePartialLoader : ExampleLimitOrderLoader
    {
        public ExampleLimitTruePartialLoader()
        {
            category = "Test";
            name = "True Partial LimitOrders";
            IsVisibleInGUI = false;
        }

        public override void OnInitialize(ProjectProperties properties) {
        }
		
        public override void OnLoad(ProjectProperties properties) {
#pragma warning disable 612,618
            foreach (var symbol in properties.Starter.SymbolProperties)
#pragma warning restore 612,618
            {
                symbol.PartialFillSimulation = PartialFillSimulation.PartialFillsIncomplete;
            }
            TopModel = GetStrategy("ExampleOrderStrategy");
        }
    }
}