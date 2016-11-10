using System;

namespace EventStoreKit.Utility
{
    public static class DateTimeUtility
    {
        public static DateTime TrimMilliseconds(this DateTime value)
        {
            return value.AddMilliseconds(-value.Millisecond);
        }
    }
}
