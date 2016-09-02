﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Services;
using EventStoreKit.Sql.PersistanceManager;
using EventStoreKit.Utility;

namespace EventStoreKit.Sql.ProjectionTemplates
{
    
    public class ProjectionTemplate<TReadModel> : IProjectionTemplate
        where TReadModel : class
    {
        #region Private fields

        protected readonly Action<Type, Action<Message>, bool> EventRegister;
        protected readonly Func<IDbProvider> PersistanceManagerFactory;
        protected readonly Dictionary<Type, IEventHandlerInitializer> EventHandlerInitializers = new Dictionary<Type, IEventHandlerInitializer>();
        protected readonly ILogger Logger;

// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private readonly ProjectionTemplateOptions Options;
        private readonly int InsertBufferSize = 5000;
// ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
        private readonly bool ReadModelCaching;
        private readonly IDbStrategy<TReadModel> DbStrategy;
        //private readonly ThreadSafeDictionary<Guid,TReadModel> Cache;
        private readonly ConcurrentDictionary<Guid,TReadModel> Cache;
        private Func<IDbProvider, Guid, object> GetByIdDelegate;

    
        #endregion

        #region Implementation of IProjectionTemplate

        public void CleanUp( SystemCleanedUpEvent msg )
        {
            if ( Cache != null )
                Cache.Clear();

            foreach ( var initializer in EventHandlerInitializers.Values )
                initializer.CleanUp();

            CreateTables( msg );
        }

        public void PreprocessEvent( Message @event )
        {
            foreach ( var initializer in EventHandlerInitializers.Values )
                initializer.PreprocessEvent( @event );
        }

        public void Flush()
        {
            foreach ( var initializer in EventHandlerInitializers.Values )
                initializer.Flush();
            Cache.Do( c => c.Clear() );
        }

        #endregion

        #region Protected methods

        protected virtual void CreateTables( SystemCleanedUpEvent msg )
        {
            PersistanceManagerFactory.Run( db => db.CreateTable<TReadModel>( overwrite: true ) );
        }

        protected void Register<TEvent>( Action<TEvent> action ) where TEvent : Message
        {
            EventRegister( typeof( TEvent ), DelegateAdjuster.CastArgument<Message, TEvent>( x => action( x ) ), false );
        }

        protected static PropertyInfo GetProperty<T>( params string[] properties )
        {
            return typeof( T ).ResolveProperty( properties );
        }

        #endregion

        public ProjectionTemplate( 
            Action<Type, Action<Message>, bool> eventRegister,
            Func<IDbProvider> dbProviderFactory,
            ILogger logger = null,
            ProjectionTemplateOptions options = ProjectionTemplateOptions.None )
        {
            EventRegister = eventRegister;
            PersistanceManagerFactory = dbProviderFactory;
            Logger = logger;

            Options = options;
            ReadModelCaching = Options.HasFlag( ProjectionTemplateOptions.ReadCachingSingle );
            if ( ReadModelCaching )
                Cache = new ConcurrentDictionary<Guid, TReadModel>();
            
            // Init DbStrategy
            if ( Options.HasFlag( ProjectionTemplateOptions.InsertCaching ) )
            {
                int.TryParse(ConfigurationManager.AppSettings["InsertBufferSize"], out InsertBufferSize);
                DbStrategy = new DbStrategyBuffered<TReadModel>(dbProviderFactory, logger, InsertBufferSize);
            }
            else
            {
                DbStrategy = new DbStrategyDirect<TReadModel>( dbProviderFactory );
            }
        }
        
        public EventHandlerInitializer<TReadModel, TEvent> InitEventHandler<TEvent>()
            where TEvent : Message
        {
            var initializer = new EventHandlerInitializer<TReadModel, TEvent>( EventRegister, PersistanceManagerFactory, DbStrategy, Cache );
            EventHandlerInitializers.Add( typeof( TEvent ), initializer );
            return initializer;
        }

        #region GetById section

        protected void InitGetByIdDelegate( PropertyInfo idProperty )
        {
            GetByIdDelegate = GenerateGetByIdDelegate<TReadModel>( idProperty );
        }

        protected Func<IDbProvider, Guid, object> GenerateGetByIdDelegate<TEntity>( PropertyInfo idProperty )
            where TEntity : class
        {
            return ( db, id ) => db.Query<TEntity>().SingleOrDefault( ExpressionsUtility.GetEqualPredicat<TEntity>( idProperty, id ) );
        }

        public TReadModel GetById( Guid id )
        {
            if ( id == Guid.Empty || GetByIdDelegate == null )
                return null;
            var result =
                ReadModelCaching ? 
                Cache.GetOrAdd( id, id1 => PersistanceManagerFactory.Run( db => (TReadModel)GetByIdDelegate( db, id1 ) ) ) :
                PersistanceManagerFactory.Run( db => (TReadModel)GetByIdDelegate( db, id ) );
            return result;
        }
        public TReadModel GetById( IDbProvider db, Guid id )
        {
            if ( id == Guid.Empty || GetByIdDelegate == null )
                return null;
            var result =
                ReadModelCaching ?
                Cache.GetOrAdd( id, id1 => (TReadModel) GetByIdDelegate( db, id1 ) ) :
                (TReadModel) GetByIdDelegate( db, id );
            return result;
        }

        #endregion

        #region Customization methods

        public ProjectionTemplate<TReadModel> InitNewEntityWith<TEvent>( Action<IDbProvider, TEvent, TReadModel> initNewEntityExpression ) where TEvent : Message
        {
            EventHandlerInitializers
                .Get( typeof( TEvent ) )
                .OfType<EventHandlerInitializer<TReadModel, TEvent>>()
                .Do( handler => handler.InitNewEntityWith( initNewEntityExpression ) );
            return this;
        }

        public ProjectionTemplate<TReadModel> UpdateWith<TEvent>( Func<IDbProvider, TEvent, Expression<Func<TReadModel, TReadModel>>> updateExpression ) where TEvent : Message
        {
            EventHandlerInitializers
                .Get( typeof (TEvent) )
                .OfType<EventHandlerInitializer<TReadModel,TEvent>>() // todo: process exeption of warning if type is wrong or there is no such handlers
                .Do( handler => handler.UpdateWith( updateExpression ) ); 
            return this;
        }

        public ProjectionTemplate<TReadModel> ValidateWith<TEvent>( Func<IDbProvider, TEvent, bool> validateExpression )  where TEvent : Message
        {
            EventHandlerInitializers
               .Get( typeof( TEvent ) )
               .OfType<EventHandlerInitializer<TReadModel, TEvent>>()
               .Do( handler => handler.ValidateWith( validateExpression ) );
            return this;
        }

        public ProjectionTemplate<TReadModel> RunAfterHandle<TEvent>( Action<IDbProvider, TEvent> afterExpression ) where TEvent : Message
        {
            EventHandlerInitializers
               .Get( typeof( TEvent ) )
               .OfType<EventHandlerInitializer<TReadModel, TEvent>>()
               .Do( handler => handler.RunAfterHandle( afterExpression ) );
            return this;
        }

        public ProjectionTemplate<TReadModel> RunBeforeHandle<TEvent>( Action<IDbProvider, TEvent> beforeExpression ) where TEvent : Message
        {
            EventHandlerInitializers
               .Get( typeof( TEvent ) )
               .OfType<EventHandlerInitializer<TReadModel, TEvent>>()
               .Do( handler => handler.RunBeforeHandle( beforeExpression ) );
            return this;
        }

        public ProjectionTemplate<TReadModel> PostProcess<TEvent>( Action<TEvent> postProcessExpression ) where TEvent : Message
        {
            EventHandlerInitializers
               .Get( typeof( TEvent ) )
               .OfType<EventHandlerInitializer<TReadModel, TEvent>>()
               .Do( handler => handler.PostProcess( postProcessExpression ) );
            return this;
        }

        #endregion
    }
}