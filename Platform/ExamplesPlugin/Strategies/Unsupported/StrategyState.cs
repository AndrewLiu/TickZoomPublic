using System;

namespace TickZoom.Examples
{
    [Flags]
    public enum StrategyState
    {
        None,
        Active = 0x01,
        HighRisk = 0x02,
        EndForWeek = 0x04,
        OverSize = 0x08,
        ProcessSizing = Active | HighRisk,
        ProcessOrders = Active | OverSize | HighRisk,
    }
    public static class LocalExtensions
    {
        public static bool AnySet(this Enum input, Enum matchInfo)
        {
            var inputInt = Convert.ToUInt32(input);
            var matchInt = Convert.ToUInt32(matchInfo);
            return ((inputInt & matchInt) != 0);
        }
    }

}