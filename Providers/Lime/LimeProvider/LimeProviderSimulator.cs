using TickZoom.Api;
using TickZoom.Provider.FIX;

namespace TickZoom.Provider.LimeFIX 
{
    class LimeProviderSimulator : ProviderSimulatorSupport
    {
        public LimeProviderSimulator(string mode, ProjectProperties projectProperties)
            : base(mode, projectProperties, typeof(LimeFIXSimulator), typeof(LimeQuotesSimulator))
        {
            
        } }
}
