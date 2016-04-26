using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using EventStoreKit.Messages;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;
using EventStoreKit.Sql.PersistanceManager;
using EventStoreKit.Sql.ProjectionTemplates;
using EventStoreKit.Utility;
using log4net;

namespace EventStoreKit.Sql.Projections
{
    public abstract class SqlProjectionBase : ProjectionBase
    {
        #region Protected fields
        
        protected readonly Func<IDbProvider> DbProviderFactory;
        
        #endregion

        #region Private methods

        private TTemplate CreateTemplate<TTemplate>( Action<Type, Action<Message>, bool> register, Func<IDbProvider> dbFactory, bool caching )
            where TTemplate : IProjectionTemplate
        {
            var ttype = typeof (TTemplate);
            var ctor = ttype
                .GetConstructor( new[]
                {
                    typeof (Action<Type, Action<Message>, bool>),
                    typeof (Func<IDbProvider>),
                    typeof (bool)
                } );
            if( ctor == null )
                throw new InvalidOperationException( ttype.Name + " doesn't have constructor ( Action<Type,Action<Message>,bool>, Func<IPerdistanceManagerProjection> )" );
            return (TTemplate)( ctor.Invoke( new object[] { register, dbFactory, caching } ) );
        }

        #endregion

        protected SqlProjectionBase(
            ILog logger, 
            IScheduler scheduler,
            Func<IDbProvider> dbProviderFactory )
            : base( logger, scheduler )
        {
            DbProviderFactory = dbProviderFactory.CheckNull( "dbProviderFactory" );
        }

        protected TTemplate RegisterTemplate<TTemplate>( bool readModelCaching = false ) where TTemplate : IProjectionTemplate
        {
            var template = CreateTemplate<TTemplate>( Register, DbProviderFactory, readModelCaching );
            ProjectionTemplates.Add( template );
            return template;
        }

        #region Filters & Sorters

        protected Dictionary<string, Func<SearchFilterInfo, Expression<Func<TModel, bool>>>> InitializeFilters<TModel>() where TModel : class
        {
            var type = typeof( TModel );
            var dict = new Dictionary<string, Func<SearchFilterInfo, Expression<Func<TModel, bool>>>>();
            foreach ( var property in type.GetProperties() )
            {
                //if ( property.GetCustomAttributes( typeof( SqlIgnoreAttribute ), false ).Length > 0 )
                //    continue;
                var filter = property.GetFilterExpression<TModel>();
                if ( filter != null )
                    dict.Add( property.Name.ToLower(), filter );
            }
            return dict;
        }
        protected Dictionary<string, Expression<Func<TModel, object>>> InitializeSorters<TModel>() where TModel : class
        {
            var type = typeof( TModel );
            var dict = new Dictionary<string, Expression<Func<TModel, object>>>();
            foreach ( var property in type.GetProperties() )
            {
                //if ( property.GetCustomAttributes( typeof( SqlIgnoreAttribute ), false ).Length > 0 )
                //    continue;
                dict.Add( property.Name.ToLower(), property.GetAccessExpression<TModel>() );
            }
            return dict;
        }

        #endregion

    }

    public abstract class SqlProjectionBase<TModel> : SqlProjectionBase where TModel : class
    {
        #region Private fields

        private readonly Dictionary<string, Expression<Func<TModel, object>>> SorterMapping;
        private readonly Dictionary<string, Func<SearchFilterInfo, Expression<Func<TModel, bool>>>> FilterMapping;

        #endregion

        protected SqlProjectionBase(
            ILog logger, 
            IScheduler scheduler,
            Func<IDbProvider> dbProviderFactory ) : 
            base( logger, scheduler, dbProviderFactory )
        {
            SorterMapping = InitializeSorters<TModel>();
            FilterMapping = InitializeFilters<TModel>();
        }

        protected Dictionary<string, Func<SearchFilterInfo, Expression<Func<TModel, bool>>>> InitializeFilters() { return FilterMapping; }
        protected Dictionary<string, Expression<Func<TModel, object>>> InitializeSorters() { return SorterMapping; }

        public QueryResult<TModel> Search(
            SearchOptions.SearchOptions options,
            ICurrentUserProvider currentUserProvider = null, // required for IOrganizationModel
            Func<TModel, TModel, TModel> summaryAggregate = null ) // required for summary
        {
            return DbProviderFactory.Run( db => db.PerformQuery(
                options, 
                FilterMapping,
                SorterMapping,
                currentUserProvider,
                summaryAggregate: summaryAggregate ) );
        }

        public QueryResult<TResult> Search<TResult>(
            SearchOptions.SearchOptions options,
            Func<IDbProvider,Func<TModel, TResult>> getEvaluator,
            ICurrentUserProvider currentUserProvider = null, // required for IOrganizationModel
            Func<TModel, TModel, TModel> summaryAggregate = null ) // required for summary
        {
            return DbProviderFactory.Run( db =>
            {
                var evaluator = getEvaluator( db );
                var result = db.PerformQuery( 
                    options,
                    FilterMapping,
                    SorterMapping,
                    currentUserProvider,
                    summaryAggregate: summaryAggregate );
                return new QueryResult<TResult>( 
                    result.Select( evaluator ).ToList(), 
                    options, 
                    result.Total,
                    result.Summary.With( evaluator ) );
            } );
        }

        public QueryResult<TModel> GetList( 
            Expression<Func<TModel,bool>> predicat = null,
            Func<TModel, TModel, TModel> summaryAggregate = null )
        {
            var result = DbProviderFactory.Run( db =>
            {
                var query = db.Query<TModel>();
                if ( predicat != null )
                    query = query.Where( predicat );
                return query.ToList();
            } );
            
            TModel summary = null;
            if ( summaryAggregate != null )
                summary = ( result.Any() ) ? result.Aggregate( summaryAggregate ) : Activator.CreateInstance<TModel>();

            return new QueryResult<TModel>( result, new SearchOptions.SearchOptions(), total: result.Count(), summary: summary );
        }
    }
}
