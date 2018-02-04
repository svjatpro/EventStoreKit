using System;
using System.Collections.Generic;
using System.Linq;
using NEventStore;
using NEventStore.Persistence;

namespace EventStoreKit.Services.ReplayHistory
{
    public enum ReplayHistoryInterval
    {
        Year,
        Month
    }

    public class CommitsIteratorByPeriod : ICommitsIterator
    {
        private readonly IStoreEvents Store;
        private readonly ReplayHistoryInterval Interval;
        private DateTime? DateFrom;

        public void Reset()
        {
            DateFrom = null;
        }

        public List<ICommit> LoadNext()
        {
            if ( DateFrom == null )
                DateFrom = new DateTime( 2015, 1, 1 );
            var result = new List<ICommit>();

            while ( !result.Any() && DateFrom <= DateTime.Now )
            {
                DateTime dateTo;
                switch ( Interval )
                {
                    case ReplayHistoryInterval.Year:
                        dateTo = DateFrom.Value.AddYears( 1 );
                        break;
                    case ReplayHistoryInterval.Month:
                        dateTo = DateFrom.Value.AddMonths( 1 );
                        break;
                    default:
                        dateTo = DateTime.Now.AddDays( 1 );
                        break;
                }
                IEnumerable<ICommit> query;
                if ( dateTo > DateTime.Now )
                {
                    query = Store.Advanced.GetFrom( DateFrom.Value );
                    DateFrom = dateTo;
                }
                else
                {
                    query = Store.Advanced.GetFromTo( Bucket.Default, DateFrom.Value, dateTo );
                    DateFrom = dateTo;
                }
                result = query.ToList();
            }

            if ( !result.Any() )
                DateFrom = null;
            return result;
        }

        public CommitsIteratorByPeriod( 
            IStoreEvents store, 
            ReplayHistoryInterval interval = ReplayHistoryInterval.Month )
        {
            Store = store;
            Interval = interval;
        }
    }
}
