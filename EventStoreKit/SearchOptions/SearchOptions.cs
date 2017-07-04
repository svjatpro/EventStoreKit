using System.Collections.Generic;
using EventStoreKit.Constants;
using EventStoreKit.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            IList<SearchFilterInfo> filters = null,
            string sort = null,
            string group = null,
            string quickSearch = null )
        {
            if ( filters == null )
                filters = new List<SearchFilterInfo>();
            foreach ( var filter in filters )
            {
                var value = filter.Data.Value as JObject;
                if ( value != null )
                {
                    foreach ( var token in value )
                    {
                        if ( token.Key == SearchComparisonType.After )
                        {
                            filter.Data.Value = token.Value.Value<string>();
                            filter.Data.Comparison = SearchComparisonType.After;
                        }
                        else if ( token.Key == SearchComparisonType.Before )
                        {
                            filter.Data.Value = token.Value.Value<string>();
                            filter.Data.Comparison = SearchComparisonType.Before;
                        }
                        else if ( token.Key == SearchComparisonType.On )
                        {
                            filter.Data.Value = token.Value.Value<string>();
                            filter.Data.Comparison = SearchComparisonType.On;
                        }
                    }
                }
            }

            if ( quickSearch != null )
            {
                filters.Add( new SearchFilterInfo
                {
                    Data = new SearchFilterInfo.SearchFilterData { Type = "string", Value = quickSearch },
                    Field = SearchConstants.QuickSearch
                } );
            }

            var sorters = sort.With( JsonConvert.DeserializeObject<List<SorterInfo>> ) ?? new List<SorterInfo>();
            var groupers = group.With( JsonConvert.DeserializeObject<List<SorterInfo>> ) ?? new List<SorterInfo>();

            return
                page.HasValue && limit.HasValue ?
                new SearchOptions( page.Value, limit.Value, filters: filters, sorters: sorters, groupers: groupers ) :
                new SearchOptions( filters: filters, sorters: sorters, groupers: groupers );
        }

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
