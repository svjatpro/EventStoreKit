using System;
using System.Collections.Generic;
using System.Threading;

namespace EventStoreKit.Utility
{
    public static class DateTimeUtility
    {
        #region Private members

        private static Dictionary<int, string> MonthMapping = 
            new Dictionary<int, string>
            {
                { 1, "січні" },
                { 2, "лютому" },
                { 3, "березні" },
                { 4, "квітні" },
                { 5, "травні" },
                { 6, "червні" },
                { 7, "липні" },
                { 8, "серпні" },
                { 9, "вересні" },
                { 10, "жовтні" },
                { 11, "листопаді" },
                { 12, "грудні" }
            };

        #endregion

        public static DateTime TrimMilliseconds(this DateTime value)
        {
            return value.AddMilliseconds(-value.Millisecond);
        }

        public static DateTime? TrimMilliseconds(this DateTime? value)
        {
            return value.HasValue ? value.Value.TrimMilliseconds() : value;
        }

        public static DateTime TrimTime( this DateTime value )
        {
            return new DateTime( value.Year, value.Month, value.Day );
        }

        public static string GetMonthPreposition( this DateTime value )
        {
            return MonthMapping[value.Month];
        }

        public static string GetMonth( this DateTime value )
        {
            return Thread.CurrentThread.CurrentCulture.DateTimeFormat.GetMonthName( value.Month );
        }
    }
}
