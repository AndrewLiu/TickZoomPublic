using System;
using System.Reflection;
using TickZoom.Api;
using TickZoom.LimeFIX;
using TickZoom.LimeQuotes;

namespace TickZoom.LimeFix
{
    public class LimeAssemblyFactory : AssemblyFactory
    {
        public AgentPerformer CreatePerformer(string className, params object[] args)
        {
            switch( className) {
                case "ExecutionProvider":
                    return (AgentPerformer)Factory.Parallel.Spawn(typeof(LimeFIXProvider), args);
                case "DataProvider":
                    return (AgentPerformer)Factory.Parallel.Spawn(typeof(LimeQuotesProvider), args);
                case "ProviderSimulator":
                    return (AgentPerformer)Factory.Parallel.Spawn(typeof(LimeProviderSimulator), args);
                default:
                    throw new ApplicationException("Unexpected type to construct: " + className);

            }
        }
    }
}