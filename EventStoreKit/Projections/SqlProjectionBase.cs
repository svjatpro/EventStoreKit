using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using System.Reflection;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections.MessageHandler;
using EventStoreKit.ProjectionTemplates;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;
using EventStoreKit.Utility;

namespace EventStoreKit.Projections
{
    public abstract class SqlProjectionBase : EventQueueSubscriber
    {
        #region Protected fields

        private readonly HashSet<Type> ReadModels = new HashSet<Type>();
        private readonly List<IProjectionTemplate> ProjectionTemplates = new List<IProjectionTemplate>();

        protected readonly Func<IDbProvider> DbProviderFactory;

        #endregion

        #region Private methods

        private void CleanUpProjection( SystemCleanedUpEvent message )
        {
            DbProviderFactory.Run( db =>
            {
                var createTableMethod = db.GetType().GetMethod( "CreateTable", BindingFlags.Public | BindingFlags.Instance );
                GetReadModels().ForEach( modelType =>
                {
                    createTableMethod.MakeGenericMethod( modelType ).Invoke( db, new object[] { true } );
                } );
            } );
                
            OnCleanup( message );
        }

        private TTemplate CreateTemplate<TTemplate>( Action<Type, Action<Message>, ActionMergeMethod> register, Func<IDbProvider> dbFactory, ILogger log, ProjectionTemplateOptions options )
            where TTemplate : IProjectionTemplate
        {
            var ttype = typeof (TTemplate);
            var ctor = ttype
                .GetConstructor( new[]
                {
                    typeof (Action<Type, Action<Message>, ActionMergeMethod>),
                    typeof (Func<IDbProvider>),
                    typeof (ILogger),
                    typeof (ProjectionTemplateOptions)
                } );
            if( ctor == null )
                throw new InvalidOperationException( ttype.Name + " doesn't have constructor ( Action<Type,Action<Message>,bool>, Func<IPerdistanceManagerProjection> )" );
            return (TTemplate)( ctor.Invoke( new object[] { register, dbFactory, log, options } ) );
        }

        private void InitReadModel( IDbProvider db, Type modelType )
        {
            var createTableMethod = db.GetType().GetMethod( "CreateTable", BindingFlags.Public | BindingFlags.Instance );
            createTableMethod.MakeGenericMethod( modelType ).Invoke( db, new object[] { false } );
        }

        #endregion

        #region Protected methods

        protected void Flush()
        {
            ProjectionTemplates.ForEach( t => t.Flush() );
        }
        
        #region ReadModels

        protected void RegisterReadModel<TReadModel>()
        {
            RegisterReadModel( typeof( TReadModel ) );
        }
        protected void RegisterReadModel( Type tModel )
        {
            if ( !ReadModels.Contains( tModel ) )
            {
                ReadModels.Add( tModel );
                DbProviderFactory.Run( db => InitReadModel( db, tModel ) );
            }
        }
        protected List<Type> GetReadModels()
        {
            return ReadModels.ToList();
        }

        #endregion

        #region ProjectionTemplates

        protected TTemplate RegisterTemplate<TTemplate>( TTemplate template ) where TTemplate : IProjectionTemplate
        {
            template.GetReadModels().ToList().ForEach( RegisterReadModel );
            ProjectionTemplates.Add( template );
            return template;
        }
        protected TTemplate RegisterTemplate<TTemplate>( ProjectionTemplateOptions options = ProjectionTemplateOptions.None ) where TTemplate : IProjectionTemplate
        {
            var template = CreateTemplate<TTemplate>( Register, DbProviderFactory, Log, options );
            RegisterTemplate( template );
            return template;
        }
        protected ProjectionTemplate<TModel> RegisterGenericTemplate<TModel>( ProjectionTemplateOptions options = ProjectionTemplateOptions.None ) where TModel : class
        {
            var template = CreateTemplate<ProjectionTemplate<TModel>>( Register, DbProviderFactory, Log, options );
            RegisterTemplate( template );
            return template;
        }
        protected List<IProjectionTemplate> GetTemplates()
        {
            return ProjectionTemplates.ToList();
        }

        #endregion

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

        protected override void PreprocessMessage( Message message )
        {
            ProjectionTemplates.ForEach( t => t.PreprocessEvent( message ) );
        }

        protected virtual void OnCleanup( SystemCleanedUpEvent message ) { }
        
        #endregion
        
        protected SqlProjectionBase(
            ILogger logger, 
            IScheduler scheduler,
            Func<IDbProvider> dbProviderFactory )
            : base( logger, scheduler )
        {
            DbProviderFactory = dbProviderFactory.CheckNull( "dbProviderFactory" );

            Register<SystemCleanedUpEvent>( CleanUpProjection );
            Register<SequenceMarkerEvent>( m => Flush(), ActionMergeMethod.MultipleRunBefore );
            Register<StreamOnIdleEvent>( m => Flush(), ActionMergeMethod.MultipleRunBefore );
        }
    }

    public abstract class SqlProjectionBase<TModel> : SqlProjectionBase where TModel : class
    {
        #region Private fields

        protected readonly Dictionary<string, Expression<Func<TModel, object>>> SorterMapping;
        protected readonly Dictionary<string, Func<SearchFilterInfo, Expression<Func<TModel, bool>>>> FilterMapping;

        #endregion

        protected SqlProjectionBase(
            ILogger logger, 
            IScheduler scheduler,
            Func<IDbProvider> dbProviderFactory ) : 
            base( logger, scheduler, dbProviderFactory )
        {
            RegisterReadModel<TModel>();

            SorterMapping = InitializeSorters<TModel>();
            FilterMapping = InitializeFilters<TModel>();
        }

        protected Dictionary<string, Func<SearchFilterInfo, Expression<Func<TModel, bool>>>> InitializeFilters() { return FilterMapping; }
        protected Dictionary<string, Expression<Func<TModel, object>>> InitializeSorters() { return SorterMapping; }

        public QueryResult<TModel> Search(
            SearchOptions.SearchOptions options,
            ICurrentUserProvider currentUserProvider = null, // required for IOrganizationModel
            Func<TModel, TModel, TModel> summaryAggregate = null,  // required for summary
            SummaryCache<TModel> summaryCache = null )  // required for summary caching
        {
            return DbProviderFactory.Run( db => db.PerformQuery(
                options, 
                FilterMapping,
                SorterMapping,
                summaryAggregate: summaryAggregate,
                summaryCache: summaryCache ) );
        }

        public QueryResult<TResult> Search<TResult>(
            SearchOptions.SearchOptions options,
            Func<IDbProvider,Func<TModel, TResult>> getEvaluator,
            ICurrentUserProvider currentUserProvider = null, // required for IOrganizationModel
            Func<TModel, TModel, TModel> summaryAggregate = null, // required for summary
            SummaryCache<TModel> summaryCache = null )  // required for summary caching
        {
            return DbProviderFactory.Run( db =>
            {
                var evaluator = getEvaluator( db );
                var result = db.PerformQuery( 
                    options,
                    FilterMapping,
                    SorterMapping,
                    summaryAggregate: summaryAggregate,
                    summaryCache: summaryCache );
                return new QueryResult<TResult>( 
                    result.Select( evaluator ).ToList(), 
                    options,
                    result.Total,
                    result.Summary.With( evaluator ) );
            } );
        }

        public QueryResult<TModel> GetList( 
            Expression<Func<TModel,bool>> predicat = null,
            Func<TModel, TModel, TModel> summaryAggregate = null,
            SummaryCache<TModel> summaryCache = null )
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
                summary = result.Any() ? result.Aggregate( summaryAggregate ) : Activator.CreateInstance<TModel>();

            return new QueryResult<TModel>( result, new SearchOptions.SearchOptions(), total: result.Count, summary: summary );
        }
    }
}
