using System;

namespace TickZoom.Api
{
    public static class Cloner<T>
    {
        private static Func<T, T> cloner;
        public static T Clone(T myObject)
        {
            if (cloner == null) cloner = (Func<T, T>)CloneHelper.GetDelegate(typeof(T));
            return cloner(myObject);
        }

    }
}