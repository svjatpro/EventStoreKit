using System.Collections.Generic;

namespace OSMD.Domain
{
    public class SearchOptions
    {
        public readonly int PageIndex;
        public int PageSize;
        public readonly IList<SearchFilterInfo> Filters;
        public readonly IList<SorterInfo> Sorters;
        public readonly IList<SorterInfo> Groupers;

        public SearchOptions( IList<SearchFilterInfo> filters = null, IList<SorterInfo> sorters = null, IList<SorterInfo> groupers = null  )
        {
            PageIndex = 0;
            PageSize = 0;
            Filters = filters;
            Sorters = sorters;
            Groupers = groupers;
        }
        public SearchOptions( int pageIndex, int pageSize, IList<SearchFilterInfo> filters = null, IList<SorterInfo> sorters = null, IList<SorterInfo> groupers = null  )
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            Filters = filters;
            Sorters = sorters;
            Groupers = groupers;
        }
    }
}
