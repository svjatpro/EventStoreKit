using System.Collections.Generic;

namespace EventStoreKit.SearchOptions
{
    public class SearchOptions
    {
        public readonly int PageIndex;
        public int PageSize;
        public readonly IList<SearchFilterInfo> Filters;
        public readonly IList<SorterInfo> Sorters;
        public readonly IList<SorterInfo> Groupers;

        public SearchOptions( IList<SearchFilterInfo> filters = null, IList<SorterInfo> sorters = null, IList<SorterInfo> groupers = null )
            : this( 0, 0, filters, sorters, groupers )
        {
        }
        public SearchOptions( int pageIndex, int pageSize, IList<SearchFilterInfo> filters = null, IList<SorterInfo> sorters = null, IList<SorterInfo> groupers = null  )
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            Filters = filters ?? new List<SearchFilterInfo>();
            Sorters = sorters ?? new List<SorterInfo>();
            Groupers = groupers ?? new List<SorterInfo>();
        }
    }
}
