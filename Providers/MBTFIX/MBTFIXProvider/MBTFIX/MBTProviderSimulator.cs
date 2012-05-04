using TickZoom.Api;
using TickZoom.Provider.FIX;

namespace TickZoom.Provider.MBTFIX
{
    public class MBTProviderSimulator : ProviderSimulatorSupport
    {
        public MBTProviderSimulator(string mode, ProjectProperties projectProperties)
            : base(mode, projectProperties, typeof(MBTFIXSimulator), typeof(MBTQuoteSimulator))
        {
            
        }
    }
}