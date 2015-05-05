using System.Collections;
using System.Collections.Generic;

namespace EventStoreKit.SearchOptions
{
    /// <summary>
    /// Wrapper for IEnumerable<T/> with base SearchOptions and TotalCount
    /// </summary>
    public class QueryResult<TEntity> : IEnumerable<TEntity>
    {
        #region IEnumerable<TEntity> implementation

        public IEnumerator<TEntity> GetEnumerator()
        {
            return Source.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        public readonly SearchOptions SearchOptions;
        public readonly IEnumerable<TEntity> Source;
        public readonly int Total;
        public readonly TEntity Summary;

        public QueryResult( IEnumerable<TEntity> source, SearchOptions options, int total = 0, TEntity summary = default( TEntity ) )
        {
            Source = source;
            SearchOptions = options;
            Total = total;
            Summary = summary;
        }
    }
}
