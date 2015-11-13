using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EventStoreKit.Messages;
using EventStoreKit.Services;
using EventStoreKit.Sql.PersistanceManager;
using EventStoreKit.Utility;

namespace EventStoreKit.Sql.ProjectionTemplates
{
    public abstract class ProjectionTemplate : IProjectionTemplate
    {
        #region Private members

        protected readonly Action<Type, Action<Message>, bool> EventRegister;
        protected readonly Func<IDbProviderProjection> PersistanceManagerFactory;
        protected readonly Dictionary<Type, IEventHandlerInitializer> EventHandlerInitializers = new Dictionary<Type, IEventHandlerInitializer>();

        #endregion

        #region Implementation of IProjectionTemplate

        public virtual void CleanUp( SystemCleanedUpEvent msg )
        {
            foreach ( var initializer in EventHandlerInitializers.Values )
                initializer.CleanUp();
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
        }

        #endregion

        protected ProjectionTemplate( Action<Type, Action<Message>, bool> eventRegister, Func<IDbProviderProjection> persistanceManagerFactory )
        {
            EventRegister = eventRegister;
            PersistanceManagerFactory = persistanceManagerFactory;
        }

        #region Protected methods

        protected void Register<TEvent>( Action<TEvent> action ) where TEvent : Message
        {
            EventRegister( typeof( TEvent ), DelegateAdjuster.CastArgument<Message, TEvent>( x => action( x ) ), false );
        }

        protected static PropertyInfo GetProperty<T>( params string[] properties )
        {
            return typeof( T ).ResolveProperty( properties );
        }

        protected EventHandlerInitializer<TReadModel, TEvent> InitEventHandler<TReadModel, TEvent>()
            where TReadModel : class
            where TEvent : Message
        {
            var initializer = new EventHandlerInitializer<TReadModel, TEvent>( EventRegister, PersistanceManagerFactory );
            EventHandlerInitializers.Add( typeof( TEvent ), initializer );
            return initializer;
        }

        #endregion
    }

    public class ProjectionTemplate<TReadModel> : ProjectionTemplate
        where TReadModel : class
    {
        #region Private fields

        private readonly bool ReadModelCaching;
        private readonly ThreadSafeDictionary<Guid,TReadModel> Cache;
        private Func<IDbProvider, Guid, object> GetByIdDelegate;

        #endregion

        public override void CleanUp( SystemCleanedUpEvent msg )
        {
            if ( Cache != null )
                Cache.Clear();
            base.CleanUp( msg );
            CreateTables( msg );
        }

        #region Protected methods

        protected virtual void CreateTables( SystemCleanedUpEvent msg )
        {
            PersistanceManagerFactory.Run( db => db.CreateTable<TReadModel>( overwrite: true ) );
        }

        #endregion

        public ProjectionTemplate( 
            Action<Type, Action<Message>, bool> eventRegister,
            Func<IDbProviderProjection> persistanceManagerFactory,
            bool readModelCaching = false ): 
            base( eventRegister, persistanceManagerFactory )
        {
            ReadModelCaching = readModelCaching;
            if ( readModelCaching )
                Cache = new ThreadSafeDictionary<Guid, TReadModel>();
        }

        public EventHandlerInitializer<TReadModel, TEvent> InitEventHandler<TEvent>()
            where TEvent : Message
        {
            return InitEventHandler<TReadModel, TEvent>();
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
        //protected Func<IDbProvider, Guid, Guid, object> GenerateGetByIdDelegate<TEntity>( PropertyInfo idProperty1, PropertyInfo idProperty2 )
        //    where TEntity : class
        //{
        //    return ( db, id1, id2 ) => db.Query<TEntity>().SingleOrDefault( ExpressionsUtility.GetEqualPredicat<TEntity>(
        //        new[] { idProperty1, idProperty2 }, new object[] { id1, id2 } ) );
        //}

        //protected Func<IDbProvider, Guid, Guid, Guid, object> GenerateGetByIdDelegate<TEntity>( PropertyInfo idProperty1, PropertyInfo idProperty2, PropertyInfo idProperty3 )
        //    where TEntity : class
        //{
        //    return ( db, id1, id2, id3 ) => db.Query<TEntity>().SingleOrDefault(
        //        ExpressionsUtility.GetEqualPredicat<TEntity>( new[] { idProperty1, idProperty2, idProperty3 }, new object[] { id1, id2, id3 } ) );
        //}

        public TReadModel GetById( Guid id )
        {
            if ( id == Guid.Empty )
                return null;
            TReadModel entity;
            return
                ( ReadModelCaching && Cache.TryGetValue( id, out entity ) ) ? 
                entity :
                GetByIdDelegate.With( d => PersistanceManagerFactory.Run( db => (TReadModel)d( db, id ) ) );
        }
        public TReadModel GetById( IDbProvider db, Guid id )
        {
            if ( id == Guid.Empty )
                return null;
            TReadModel entity;
            return
                ( ReadModelCaching && Cache.TryGetValue( id, out entity ) ) ?
                entity : 
                GetByIdDelegate.With( d => (TReadModel)d( db, id ) );
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