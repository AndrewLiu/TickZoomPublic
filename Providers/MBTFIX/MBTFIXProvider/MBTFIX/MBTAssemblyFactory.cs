using System;
using TickZoom.Api;
using TickZoom.MBTQuotes;

namespace TickZoom.MBTFIX
{
    public class MBTAssemblyFactory : AssemblyFactory
    {
        public AgentPerformer CreatePerformer(string className, params object[] args)
        {
            switch (className)
            {
                case "ExecutionProvider":
                    return (AgentPerformer)Factory.Parallel.Spawn(typeof(MBTFIXProvider), args);
                case "DataProvider":
                    return (AgentPerformer)Factory.Parallel.Spawn(typeof(MBTQuotesProvider), args);
                case "ProviderSimulator":
                    return (AgentPerformer)Factory.Parallel.Spawn(typeof(MBTProviderSimulator), args);
                default:
                    throw new ApplicationException("Unexpected type to construct: " + className);

            }
        }
    }
}