using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using EventStoreKit.Constants;
using EventStoreKit.ReadModels;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;
using EventStoreKit.Utility;

namespace EventStoreKit.Sql.PersistanceManager
{
    public static class DbProviderUtility
    {
        /// <summary>
        /// Performs action/method with separate instance of DbProvider within Sql Transaction on demand
        /// </summary>
        public static void RunLazy( this Func<IDbProvider> persistanceManagerCreator, Action<IDbProvider> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted )
        {
            using ( var persistanceManager = new DbProviderProxy( persistanceManagerCreator ) )
            {
                try
                {
                    persistanceManager.BeginTransaction( isolationLevel );
                    action( persistanceManager );
                    persistanceManager.CommitTransaction();
                }
                catch ( Exception )
                {
                    persistanceManager.RollbackTransaction();
                    throw;
                }
            }
        }

        /// <summary>
        /// Performs action/method with separate instance of DbProvider within Sql Transaction
        /// </summary>
        public static void Run( this Func<IDbProvider> persistanceManagerCreator, Action<IDbProvider> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted )
        {
            using ( var persistanceManager = persistanceManagerCreator() )
            {
                try
                {
                    persistanceManager.BeginTransaction( isolationLevel );
                    action( persistanceManager );
                    persistanceManager.CommitTransaction();
                }
                catch ( Exception )
                {
                    persistanceManager.RollbackTransaction();
                    throw;
                }
            }
        }
        /// <summary>
        /// Performs action/method with separate instance of DbProvider within Sql Transaction.
        ///   If the result is query result / list, then use ToList(). 
        ///   The reason is, than deffered materialization will be failed because of disposed connection ( and commited transaction )
        /// </summary>
        public static TResult Run<TResult>( this Func<IDbProvider> persistanceManagerCreator, Func<IDbProvider, TResult> action, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted )
        {
            using ( var persistanceManager = persistanceManagerCreator() )
            {
                try
                {
                    persistanceManager.BeginTransaction( isolationLevel );
                    var result = action( persistanceManager );
                    persistanceManager.CommitTransaction();
                    return result;
                }
                catch ( Exception )
                {
                    persistanceManager.RollbackTransaction();
                    throw;
                }
            }
        }

        public static IQueryable<TEntity> PerformQueryLazy<TEntity>(
            this IDbProvider db, 
            SearchOptions.SearchOptions options, 
            Dictionary<string, Func<SearchFilterInfo, Expression<Func<TEntity, bool>>>> filterMapping = null,
            Dictionary<string, Expression<Func<TEntity, object>>> sorterMapping = null,
            ISecurityManager securityManager = null )
            where TEntity : class
        {
            var query = db.Query<TEntity>();

            // Organization related entity
            var currentUser = securityManager.With( sm => sm.CurrentUser );
            if ( typeof( IOrganizationReadModel ).IsAssignableFrom( typeof( TEntity ) ) && currentUser != null )
            {
                options.EnsureFilterAtStart<IOrganizationReadModel>( 
                    e => e.OrganizationId,
                    () => new SearchFilterInfo.SearchFilterData{ Value = currentUser.OrganizationId } );
            }

            // apply filters
            if ( options != null && options.Filters != null )
            {
                if ( filterMapping != null )
                {
                    foreach ( var filter in options.Filters )
                    {
                        var field = filter.Field.ToLower();
                        if ( filterMapping.ContainsKey( field ) )
                            query = query.Where( filterMapping[field]( filter ) );
                    }
                }
            }

            // apply sorters
            var sorters = options.With( o => o.Sorters ).With( s => s.ToList() ) ?? new List<SorterInfo>();
            // use groupers also for sorting
            if ( options.With( o => o.Groupers ).With( g => g.Any() ) )
            {
                var grouperSort = options.Groupers.Where( g => !sorters.Any( s => s.Property == g.Property ) ).ToList();
                sorters.InsertRange( 0, grouperSort );
            }
            //if ( options.IsNotNull() && options.Sorters.With( s => s.Any() ) && sorterMapping.IsNotNull() )
            if ( sorters.Any() && sorterMapping != null )
            {
                //var sorters = options.Sorters.ToList();
                for ( var index = 0; index < sorters.Count; index++ )
                {
                    var sorter = sorters[index];
                    var sortExpression = sorterMapping[sorter.Property.ToLower()];
                    if ( index == 0 )
                    {
                        if ( sorter.Direction == SorterDirectionType.Descending )
                            query = query.OrderByDescending( sortExpression );
                        else
                            query = query.OrderBy( sortExpression );
                    }
                    else
                    {
                        if ( sorter.Direction == SorterDirectionType.Descending )
                            query = ( (IOrderedQueryable<TEntity>) query ).ThenByDescending( sortExpression );
                        else
                            query = ( (IOrderedQueryable<TEntity>) query ).ThenBy( sortExpression );
                    }
                }
            }

            return query;
        }

        public static IQueryable<TEntity> ApplyPaging<TEntity>( this IQueryable<TEntity> source, SearchOptions.SearchOptions options, int total )
        {
            if ( options != null && options.PageSize > 0 )
            {
                var start = ( options.PageIndex - 1 ) * options.PageSize;
                // if the result page is beyond of the scope, then we return first page, 
                //  the client code should be able to handle this case
                if ( total <= start )
                    start = 0;
                source = source.Skip( start );
                if ( options.PageSize > 0 )
                    source = source.Take( options.PageSize );
            }
            return source;
        }

        public static QueryResult<TEntity> PerformQuery<TEntity>(
            this IDbProvider db, 
            SearchOptions.SearchOptions options, 
            Dictionary<string, Func<SearchFilterInfo, Expression<Func<TEntity, bool>>>> filterMapping = null,
            Dictionary<string, Expression<Func<TEntity, object>>> sorterMapping = null,
            ISecurityManager securityManager = null,
            Func<TEntity, TEntity, TEntity> summaryAggregate = null )
            where TEntity : class
        {
            IQueryable<TEntity> query = PerformQueryLazy( db, options, filterMapping, sorterMapping, securityManager );
            // calculate total result count
            var total = query.Count();

            // calculate summary
            TEntity summary = null;
            if ( summaryAggregate != null )
                summary = ( total > 0 ) ? query.ToList().Aggregate( summaryAggregate ) : Activator.CreateInstance<TEntity>();

            // apply paging
            query = query.ApplyPaging( options, total );

            // return result as QueryResult<> with Total and source SearchOptions
            var result = query.ToList();
            
            return new QueryResult<TEntity>( result, options, total: total, summary: summary );
        }

        /// <summary>
        /// Ensure that the SearchOptions have the filter, and if not, then insert appropriate filter to the begin of filters list
        /// </summary>
        public static SearchOptions.SearchOptions EnsureFilterAtStart<TEntity>( 
            this SearchOptions.SearchOptions options,
            Expression<Func<TEntity, object>> getPropertyName,
            Func<SearchFilterInfo.SearchFilterData> createfilterData,
            bool condition = true )
        {
            return options.EnsureFilter( getPropertyName, createfilterData, ( list, filterInfo ) => list.Insert( 0, filterInfo ), condition );
        }

        /// <summary>
        /// Ensure that the SearchOptions have the filter, and if not, then add appropriate filter to the filters list
        /// </summary>
        public static SearchOptions.SearchOptions EnsureFilter<TEntity>( 
            this SearchOptions.SearchOptions options,
            Expression<Func<TEntity, object>> getPropertyName, 
            Func<SearchFilterInfo.SearchFilterData> createfilterData, 
            Action<List<SearchFilterInfo>,SearchFilterInfo> addfilter = null,
            bool condition = true )
        {
            var field = getPropertyName.GetPropertyName().ToLower();
            options = options ?? new SearchOptions.SearchOptions();
            var filters = options.With( o => o.Filters ).With( f => f.ToList() ) ?? new List<SearchFilterInfo>();
            if ( condition && filters.All( i => i.Field.ToLower() != field.ToLower() ) )
            {
                if ( addfilter == null )
                    addfilter = ( list, filterInfo ) => list.Add( filterInfo );
                addfilter(
                    filters,
                    new SearchFilterInfo
                    {
                        Data = createfilterData(),
                        Field = field
                    } );
                options = new SearchOptions.SearchOptions( options.PageIndex, options.PageSize, filters, options.Sorters, options.Groupers );
            }
            return options;
        }

        public static SearchOptions.SearchOptions EnsureDefaultSorter<TEntity>( this SearchOptions.SearchOptions options, Expression<Func<TEntity,object>> getPropertyName, string direction  )
        {
            if ( !options.Sorters.Any() )
            {
                options.Sorters.Add
                    ( new SorterInfo
                    {
                        Property = getPropertyName.GetPropertyName().ToLower(),
                        Direction = direction
                    } );
            }
            return options;
        }

        public static SearchOptions.SearchOptions AddSorter<TEntity>
            ( this SearchOptions.SearchOptions options, Expression<Func<TEntity, object>> getPropertyName, string direction )
        {
            options.Sorters.Add( 
                new SorterInfo
                {
                    Property = getPropertyName.GetPropertyName().ToLower(),
                    Direction = direction
                } );
            return options;
        }


        /// <summary>
        /// Check and restrict the fields if the user has no appropriate permission
        /// </summary>
        public static QueryResult<TEntity> RestrictFields<TEntity>( this QueryResult<TEntity> queryResult, bool hasAccess, Func<TEntity, TEntity> evaluator )
            where TEntity : class
        {
            if ( !hasAccess )
            {
                queryResult = new QueryResult<TEntity>(
                    queryResult
                        .AsQueryable()
                        .Select( evaluator )
                        .ToList(),
                    queryResult.SearchOptions,
                    queryResult.Total );
            }
            return queryResult;
        }
    }
}
