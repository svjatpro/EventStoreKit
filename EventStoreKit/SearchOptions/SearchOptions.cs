using System;
using System.Collections.Generic;
using System.Linq;
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

        #region Private classes

        private class FilterValueObject
        {
            public string gt { get; set; }
            public string lt { get; set; }
            public string eq { get; set; }
        }

        #endregion

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
