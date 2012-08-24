using System;

namespace TickZoom.Examples
{
    [Flags]
    public enum StrategyState
    {
        None,
        Active = 0x01,
        Suspended = 0x02,
        EndForWeek = 0x04,
        ProcessOrders = Active,
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