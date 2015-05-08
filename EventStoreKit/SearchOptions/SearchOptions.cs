using System.Collections.Generic;
using EventStoreKit.Constants;
using EventStoreKit.Utility;
using Newtonsoft.Json;

namespace EventStoreKit.SearchOptions
{
    public class SearchOptions
    {
        public readonly int PageIndex;
        public int PageSize;
        public readonly IList<SearchFilterInfo> Filters;
        public readonly IList<SorterInfo> Sorters;
        public readonly IList<SorterInfo> Groupers;

        public static SearchOptions Init(
            int? page = null,
            int? limit = null,
            /*[ModelBinder]*/ IList<SearchFilterInfo> filter = null,
            string sort = null,
            string group = null,
            string quickSearch = null )
        {
            if ( filter == null )
                filter = new List<SearchFilterInfo>();

            if ( quickSearch != null )
            {
                filter.Add( new SearchFilterInfo
                {
                    Data = new SearchFilterInfo.SearchFilterData { Type = "string", Value = quickSearch },
                    Field = SearchConstants.QuickSearch
                } );
            }

            var sorters = sort.With( JsonConvert.DeserializeObject<List<SorterInfo>> ) ?? new List<SorterInfo>();
            var groupers = group.With( JsonConvert.DeserializeObject<List<SorterInfo>> ) ?? new List<SorterInfo>();

            return
                page.HasValue && limit.HasValue ?
                new SearchOptions( page.Value, limit.Value, filters: filter, sorters: sorters, groupers: groupers ) :
                new SearchOptions( filters: filter, sorters: sorters, groupers: groupers );
        }

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
