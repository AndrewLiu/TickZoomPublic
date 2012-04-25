using TickZoom.Api;
using TickZoom.FIX;

namespace TickZoom.LimeFIX
{
    public class LimeFIXSimulatorServer : FIXSimulatorServer
    {
        public LimeFIXSimulatorServer(string mode, ProjectProperties projectProperties, ProviderSimulatorSupport providerSimulator)
            : base(mode, projectProperties, providerSimulator, 6489, new MessageFactoryFix42())
        {
        }
    }
}