﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EventStoreKit.Messages;
using EventStoreKit.Services;
using EventStoreKit.Sql.PersistanceManager;
using EventStoreKit.Utility;
using log4net;
using log4net.Core;

namespace EventStoreKit.Sql.ProjectionTemplates
{
    public interface IEventHandlerInitializer
    {
        void PreprocessEvent( Message @event );
        void Flush();
        void CleanUp();
    }

    public class EventHandlerInitializer<TReadModel, TEvent> : IEventHandlerInitializer
        where TReadModel : class
        where TEvent : Message
    {
        #region Private fields

        private class EventFieldInfo
        {
            public Func<TEvent, bool> Validator;
            public Func<IDbProvider, TEvent, object> Getter;
        }

        private readonly ThreadSafeDictionary<Guid, TReadModel> Cache; // todo: replace with ConcurrentDictionary

        private bool UseMultipleHandlers = true;
        
        private int InsertBufferCount = 100;
        private bool FlushInsertBufferBeforeAnyOtherEvents;
        private bool BufferedInsertEnabled = true;
        private readonly HashSet<Type> IgroredEvents = new HashSet<Type>();
        private readonly HashSet<Type> EventsToFlush = new HashSet<Type>();

        private readonly Dictionary<TReadModel, TEvent> EntitiesInsertBuffer = new Dictionary<TReadModel, TEvent>();
        private readonly Action<Type, Action<Message>> EventRegister;
        private readonly Action<Type, Action<Message>> EventRegisterMultiple;
        private readonly Func<IDbProvider> DbFactory;
        private readonly IDbStrategy<TReadModel> DbStrategy;

        private readonly Dictionary<PropertyInfo, EventFieldInfo> PropertiesMap = new Dictionary<PropertyInfo, EventFieldInfo>();
        private Func<TEvent, Expression<Func<TReadModel, bool>>> ReadModelPredicat;
        private readonly Dictionary<PropertyInfo, Func<TEvent, object>> ReadModelGetters = new Dictionary<PropertyInfo, Func<TEvent, object>>();
        private Func<Action<IDbProvider, IEnumerable<TEvent>>> BufferedInsertAction;

        private Action<IDbProvider, TEvent, TReadModel> InitNewEntityExpression;
        private Func<IDbProvider, TEvent, Expression<Func<TReadModel, TReadModel>>> UpdateExpression;
        private Func<IDbProvider, TEvent, bool> ValidateExpression;
        private Action<IDbProvider, TEvent> AfterExpression;
        private Action<IDbProvider, TEvent> BeforeExpression;
        private Action<TEvent> PostProcessExpression;

        #endregion

        #region Private methods

        private Guid GetReadModelId( TEvent @event )
        {
            return (Guid)ReadModelGetters.Values.First()( @event );
        }
        private Expression<Func<TReadModel, bool>> GetReadModelPredicat( TEvent @event )
        {
            var predicat = ReadModelPredicat.With( p => p( @event ) );
            if ( predicat == null && ReadModelGetters.Any() )
            {
                var parameter = Expression.Parameter( typeof( TReadModel ), "entity" );
                var equalMethods = ReadModelGetters
                    .Select(
                        equal =>
                        Expression.Equal(
                            Expression.MakeMemberAccess( parameter, equal.Key ),
                            Expression.Constant( equal.Value( @event ) ) ) )
                    .ToList();
                var binaryExpression = equalMethods.FirstOrDefault();
                equalMethods
                    .Skip( 1 )
                    .ToList()
                    .ForEach( expr => binaryExpression = Expression.AndAlso( binaryExpression, expr ) );
                return Expression.Lambda<Func<TReadModel, bool>>( binaryExpression, parameter );
            }
            return predicat;
        }
        private void InsertEntities( IDbProvider db, bool flush )
        {
            DbStrategy.Flush(); // todo: tmp

            var bufferAny = EntitiesInsertBuffer.Any();
            if ( !BufferedInsertEnabled )
                flush = true;
            if ( EntitiesInsertBuffer.Count() >= InsertBufferCount || ( flush && bufferAny ) )
            {
                db.InsertBulk( EntitiesInsertBuffer.Select( b => b.Key ).ToList() );
                var action = BufferedInsertAction.With( a => a() );
                if ( action != null )
                    action( db, EntitiesInsertBuffer.Select( b => b.Value ).ToList() );
                EntitiesInsertBuffer.Clear();
            }
        }

        private void AddToCache( TEvent @event, TReadModel model )
        {
            if( Cache != null )
            {
                var id = GetReadModelId( @event );
                Cache.ThreadSafeAdd( id, model );
            }
        }

        private string[] ValidateDefaultPropertyName( Expression<Func<TEvent, object>> getter, string[] properties )
        {
            if ( !properties.Any() )
                properties = new[] { getter.GetPropertyName() };
            return properties;
        }

        #endregion

        #region Implementation of IEventHandlerInitializer

        public void PreprocessEvent( Message @event )
        {
            var eventType = @event.GetType();
            if( FlushInsertBufferBeforeAnyOtherEvents && 
                eventType != typeof( TEvent ) &&
                !IgroredEvents.Contains( eventType ) )
            {
                Flush();
            }
        }

        public void Flush()
        {
            DbFactory.Run( db => InsertEntities( db, true ) );
        }

        public void CleanUp()
        {
            BufferedInsertEnabled = true;
        }
        
        #endregion

        public EventHandlerInitializer(
            Action<Type, Action<Message>, bool> eventRegister, 
            Func<IDbProvider> dbFactory,
            IDbStrategy<TReadModel> dbStrategy,
            ThreadSafeDictionary<Guid, TReadModel> cache = null )
        {
            EventRegisterMultiple = ( type, action ) => eventRegister( type, action, true );
            Func<bool> isMultipleHandlers = () => UseMultipleHandlers;
            EventRegister = ( type, action ) => eventRegister( type, action, isMultipleHandlers() );
            
            DbFactory = dbFactory;
            DbStrategy = dbStrategy;
            Cache = cache;
        }

        #region WithProperty

        public EventHandlerInitializer<TReadModel, TEvent> WithProperty( Expression<Func<TEvent, object>> getter, params string[] properties )
        {
            var props = ValidateDefaultPropertyName( getter, properties );
            return WithProperty( null, ( db, @event ) => getter.Compile()( @event ), typeof (TReadModel).ResolveProperty( props ) );
        }
        public EventHandlerInitializer<TReadModel, TEvent> WithProperty( Func<TEvent, object> getter, Expression<Func<TReadModel,object>> getProperty  )
        {
            return WithProperty( null, ( db, @event ) => getter( @event ), getProperty.GetPropertyName() );
        }
        public EventHandlerInitializer<TReadModel, TEvent> WithProperty( Func<TEvent, object> getter, PropertyInfo property )
        {
            return WithProperty( null, ( db, @event ) => getter( @event ), property );
        }
        public EventHandlerInitializer<TReadModel, TEvent> WithProperty( Func<IDbProvider, TEvent, object> getter, params string[] properties )
        {
            return WithProperty( null, getter, typeof( TReadModel ).ResolveProperty( properties ) );
        }
        public EventHandlerInitializer<TReadModel, TEvent> WithProperty( Func<IDbProvider, TEvent, object> getter, PropertyInfo property )
        {
            return WithProperty( null, getter, property );
        }
        public EventHandlerInitializer<TReadModel, TEvent> WithProperty( Func<TEvent, bool> validator, Func<TEvent, object> getter, PropertyInfo property )
        {
            return WithProperty( validator, ( db, e ) => getter( e ), property );
        }
        public EventHandlerInitializer<TReadModel, TEvent> WithProperty( Func<TEvent, bool> validator, Func<IDbProvider, TEvent, object> getter, params string[] properties )
        {
            return WithProperty( validator, getter, typeof( TReadModel ).ResolveProperty( properties ) );
        }

        public EventHandlerInitializer<TReadModel, TEvent> WithProperty( Func<TEvent, bool> validator, Func<IDbProvider, TEvent, object> getter, PropertyInfo property )
        {
            if ( property != null && !PropertiesMap.ContainsKey( property ) )
                PropertiesMap.Add( property, new EventFieldInfo { Validator = validator ?? ( e => true ), Getter = getter } );
            return this;
        }

        #endregion

        public EventHandlerInitializer<TReadModel, TEvent> WithMultipleHandlers( bool isMultipleHandlers ) { UseMultipleHandlers = isMultipleHandlers; return this; }
        
        public EventHandlerInitializer<TReadModel, TEvent> WithId( Func<TEvent, object> getter, PropertyInfo property )
        {
            if ( getter != null && property != null )
                ReadModelGetters.Add( property, getter );
            return this;
        }
        public EventHandlerInitializer<TReadModel, TEvent> WithId( Func<TEvent, object> getter, Expression<Func<TReadModel, object>> getProperty )
        {
            var property = typeof( TReadModel ).ResolveProperty( getProperty.GetPropertyName() );
            if ( getter != null && property != null )
                ReadModelGetters.Add( property, getter );
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> WithInsertBufferCount( int count ) { InsertBufferCount = count; return this; }

        /// <summary>
        /// Initialize handler to insert new entity;
        /// </summary>
        public EventHandlerInitializer<TReadModel, TEvent> AsInsertAction()
        {
            var eventType = typeof (TEvent);
            var properties = PropertiesMap
                .ToDictionary(
                    prop => prop.Key,
                    prop =>
                        new Action<IDbProvider, TEvent, PropertyInfo, TReadModel>(
                            ( db, e, p, u ) =>
                            {
                                if ( prop.Value.Validator( e ) )
                                    prop.Key.SetValue( u, prop.Value.Getter( db, e ), new object[] {} );
                            } ) )
                .ToList();
            var readModelType = typeof( TReadModel );

            EventRegister(
                eventType,
                @event =>
                {
                    DbFactory.Run(
                        db =>
                        {
                            // run validation expression
                            if ( !ValidateExpression.Return( v => v( db, (TEvent) @event ), true ) )
                                return;

                            if( EventsToFlush.Contains( eventType ) )
                                DbStrategy.Flush();

                            // run custom action before insert
                            BeforeExpression.Do( a => a( db, (TEvent) @event ) );

                            var entity = (TReadModel) Activator.CreateInstance( readModelType );
                            properties.ForEach( p => p.Value( db, (TEvent) @event, p.Key, entity ) );

                            // run custom initialization for new entity
                            InitNewEntityExpression.Do( a => a( db, (TEvent) @event, entity ) );

                            DbStrategy.Insert( GetReadModelId( (TEvent)@event ), entity );
                            //db.Insert( entity );
                            //db.InsertOrReplace( entity );

                            // add to cache
                            AddToCache( (TEvent) @event, entity );

                            // run custom action after insert
                            AfterExpression.Do( a => a( db, (TEvent) @event ) );
                        } );
                    PostProcessExpression.Do( a => a( (TEvent) @event ) );
                } );
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> FlushBeforeHandle()
        {
            var type = typeof (TEvent);
            if ( !EventsToFlush.Contains( type ) )
                EventsToFlush.Add( type );
            return this;
        }

        /// <summary>
        /// Initialize handler to insert buffer on HFM import finished;
        ///  Please note, argument insertAction will be called for each insert Event, but before the record inserted into DB
        /// </summary>
        public EventHandlerInitializer<TReadModel, TEvent> AsInsertBufferedAction()
        {
            //var readModelType = typeof( TReadModel );
            //var ctor = Expression.New( readModelType );
            //var parameterDb = Expression.Parameter( typeof( IDbProvider ), "db" );
            //var parameterEvent = Expression.Parameter( typeof( TEvent ), "e" );
            //Func<IDbProvider, TEvent, TReadModel> entityCreator = ( db, e ) => (TReadModel)Activator.CreateInstance( readModelType );
            //if ( PropertiesMap.Any() )
            //{
            //    var binders = PropertiesMap
            //        // we can ignore Validation for insert evaluator, because there is not 'previous value',
            //        // so one can use ( e => e.Field1.HasValue() ? e.Field1 : false ) like expression to initalize property
            //        //.Where( p => p.Value.Validator( (TEvent)@event ) ) 
            //        .Select( p =>
            //                 {
            //                     Expression<Func<IDbProvider, TEvent, object>> expr = ( db, e ) => p.Value.Getter( db, e );
            //                     return Expression.Bind( p.Key, Expression.Convert( Expression.Invoke( expr, parameterDb, parameterEvent ), p.Key.PropertyType ) );
            //                 } )
            //        .ToList();
            //    var memberInit = Expression.MemberInit( ctor, binders );
            //    var evaluator = Expression.Lambda<Func<IDbProvider, TEvent, TReadModel>>( memberInit, parameterDb, parameterEvent );
            //    entityCreator = evaluator.Compile();
            //}
            //
            //var entity = entityCreator( db, (TEvent)@event );

            var properties = PropertiesMap
               .ToDictionary(
                   prop => prop.Key,
                   prop =>
                       new Action<IDbProvider, TEvent, PropertyInfo, TReadModel>(
                           ( db, e, p, u ) =>
                           {
                               if ( prop.Value.Validator( e ) )
                                   prop.Key.SetValue( u, prop.Value.Getter( db, e ), new object[] { } );
                           } ) )
               .ToList();
            var readModelType = typeof( TReadModel );

            // 

            EventRegister(
                typeof( TEvent ),
                @event =>
                {
                    DbFactory.RunLazy(
                        db =>
                        {
                            // run validation expression
                            if ( !ValidateExpression.Return( v => v( db, (TEvent) @event ), true ) )
                                return;

                            // run custom action before insert
                            BeforeExpression.Do( a => a( db, (TEvent) @event ) );

                            var entity = (TReadModel) Activator.CreateInstance( readModelType );
                            properties.ForEach( p => p.Value( db, (TEvent) @event, p.Key, entity ) );

                            // run custom initialization for new entity
                            InitNewEntityExpression.Do( a => a( db, (TEvent) @event, entity ) );

                            // add to cache
                            AddToCache( (TEvent) @event, entity );
                            // add to buffer and call lazy insert
                            EntitiesInsertBuffer.Add( entity, (TEvent) @event );
                            InsertEntities( db, false );

                            // run custom action after insert
                            AfterExpression.Do( a => a( db, (TEvent) @event ) );
                        } );
                    PostProcessExpression.Do( a => a( (TEvent)@event ) );
                } );
            return this;
        }

        /// <summary>
        /// Initialize handler to flush insert buffer on specified events occurse;
        /// </summary>
        public EventHandlerInitializer<TReadModel, TEvent> FlushOn( Type flushEvent, params Type[] flushEvents )
        {
            new[] { flushEvent }
                .Concat( flushEvents )
                .ToList()
                .ForEach( eventType => EventRegisterMultiple( 
                    eventType, 
                    e => DbFactory.Run( db => InsertEntities( db, true ) ) ) );
            return this;
        }
        
        /// <summary>
        /// Initialize flush when idle time occurse
        /// </summary>
        //public EventHandlerInitializer<TReadModel, TEvent> FlushOnIdle( int milliseconds )
        //{
        //    FlushInsertBufferOnIdle = true;
        //    FlushOnIdleInterval = milliseconds;
        //    return this;
        //}

        /// <summary>
        /// Initialize handler to flush insert buffer when target event sequence will be breaken and there is any other event will be handled;
        /// </summary>
        public EventHandlerInitializer<TReadModel, TEvent> FlushOnAnyOtherEvent()
        {
            FlushInsertBufferBeforeAnyOtherEvents = true;
            return this;
        }
        public EventHandlerInitializer<TReadModel, TEvent> FlushOnAnyOtherEventBeside( Type ignoredEvent, params Type[] ignoredEvents )
        {
            FlushInsertBufferBeforeAnyOtherEvents = true;
            new[] { ignoredEvent }
                .Concat( ignoredEvents )
                .ToList().ForEach(
                e =>
                {
                    if ( !IgroredEvents.Contains( e ) )
                        IgroredEvents.Add( e );
                } );
            return this;
        }
        
        /// <summary>
        /// Initialize additional custom action when imsert buffer flush
        /// </summary>
        public EventHandlerInitializer<TReadModel, TEvent> OnFlushInsertBuffer( Func<Action<IDbProvider, IEnumerable<TEvent>>> insertAction = null )
        {
            BufferedInsertAction = insertAction;
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> EnableBufferedInsertOn( Type eventType/*, params Type[] events*/ )
        {
            EventRegisterMultiple( eventType, @event => BufferedInsertEnabled = true );
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> DisableBufferedInsertOn( Type eventType/*, params Type[] events*/ )
        {
            EventRegisterMultiple( eventType, @event => BufferedInsertEnabled = false );
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> BufferInsertByDefault( bool defaultValue )
        {
            BufferedInsertEnabled = defaultValue;
            return this;
        }

        /// <summary>
        /// Initialize handler to Update entity;
        /// </summary>
        public void AsUpdateAction()
        {
            var eventType = typeof( TEvent );
            var readmodelType = typeof( TReadModel );
            var ctor = Expression.New( readmodelType );
            var update = Expression.New( readmodelType );

            EventRegister(
                eventType,
                @event =>
                {
                    DbFactory.Run(
                        db =>
                        {
                            // run validation expression
                            if ( !ValidateExpression.Return( v => v( db, (TEvent) @event ), true ) )
                                return;

                            if ( EventsToFlush.Contains( eventType ) )
                                DbStrategy.Flush();

                            //// todo: make this Expression in advance with @event as parameter instead of constant
                            //Expression<Func<TReadModel, TReadModel>> evaluator = null;
                            
                            //var parameter = udpateExpr.Return(
                            //    expr => expr.Parameters[0],
                            //    Expression.Parameter( readmodelType, "entity" ) );
                            //var customBindings = udpateExpr
                            //    .With( e => e.Body.OfType<MemberInitExpression>() )
                            //    .With( m => m.Bindings )
                            //    .Return( b => b.ToList(), new List<MemberBinding>() );

                            //if ( PropertiesMap.Any() )
                            //{
                            //    var binders = customBindings.Union( PropertiesMap
                            //        .Where( p => p.Value.Validator( (TEvent) @event ) && !customBindings.Any( b => b.Member.Name == p.Key.Name ) )
                            //        .Select(
                            //            p =>
                            //            {
                            //                var customBinding = customBindings.SingleOrDefault( b => b.Member.Name == p.Key.Name );
                            //                var valueConst = Expression.Constant( p.Value.Getter( db, (TEvent) @event ), p.Key.PropertyType );
                            //                return customBinding ?? Expression.Bind( p.Key, valueConst );
                            //            } ) )
                            //        .ToList();
                            //    var memberInit = Expression.MemberInit( ctor, binders );
                            //    evaluator = Expression.Lambda<Func<TReadModel, TReadModel>>( memberInit, parameter );
                            //}
                            
                            // run custom action before update
                            BeforeExpression.Do( a => a( db, (TEvent) @event ) );

                            var predicat = GetReadModelPredicat( (TEvent) @event );
                            var udpateExpr = UpdateExpression.With( getter => getter( db, (TEvent) @event ) );
                            var eventValues = PropertiesMap
                                .Where( p => p.Value.Validator( (TEvent) @event ) )
                                .ToDictionary( p => p.Key, p => p.Value.Getter( db, (TEvent) @event ) );
                            var expresionBuilder = new ObjectExpressionBuilder<TReadModel>( udpateExpr, eventValues );

                            DbStrategy.Update( GetReadModelId( (TEvent) @event ), predicat, expresionBuilder );
                            //DbStrategy.Update( GetReadModelId( (TEvent) @event ), predicat, evaluator );
                            // if ( predicat != null && evaluator != null )
                                // db.Update( predicat, evaluator );

                            // run custom action after update
                            AfterExpression.Do( a => a( db, (TEvent) @event ) );

                            // update cache
                            if ( predicat != null && Cache != null )
                            {
                                // todo: compile expression and use it to update existing entity without additional DB query
                                var entity = db.Query<TReadModel>().Where( predicat ).FirstOrDefault();
                                if ( entity != null )
                                {
                                    var id = GetReadModelId( (TEvent) @event );
                                    Cache.AddOrUpdate( id, entity );
                                }
                            }
                        } );
                    PostProcessExpression.Do( a => a( (TEvent)@event ) );
                } );
        }

        /// <summary>
        /// Initialize handler to Delete entity;
        /// </summary>
        public void AsDeleteAction(
            Func<Action<IDbProvider, TEvent>> deleteAction = null,
            Func<Func<TEvent, bool>> validateEvent = null )
        {
            EventRegister(
                typeof( TEvent ),
                @event =>
                {
                    DbFactory.Run(
                        db =>
                        {
                            var validate = validateEvent.With( a => a() );
                            if ( validate != null && !validate( (TEvent) @event ) )
                                return;

                            DbStrategy.Flush(); // todo: is it make sence to delete from buffer without adding to DB?

                            var predicat = GetReadModelPredicat( (TEvent) @event );
                            if ( predicat != null )
                                db.Delete( predicat );

                            // delete from cache
                            if ( predicat != null && Cache != null )
                            {
                                var id = GetReadModelId( (TEvent) @event );
                                Cache.RemoveIfExist( id );
                            }

                            // run custom action
                            var action = deleteAction.With( a => a() );
                            if ( action != null )
                                action( db, (TEvent) @event );
                        } );
                    PostProcessExpression.Do( a => a( (TEvent)@event ) );
                } );
        }

        #region Customization methods

        public void InitNewEntityWith( Action<IDbProvider, TEvent, TReadModel> initNewEntityExpression )
        {
            InitNewEntityExpression = initNewEntityExpression;
        }

        public void UpdateWith( Func<IDbProvider, TEvent, Expression<Func<TReadModel, TReadModel>>> updateExpression )
        {
            UpdateExpression = updateExpression;
        }

        public void ValidateWith( Func<IDbProvider, TEvent, bool> validateExpression )
        {
            ValidateExpression = validateExpression;
        }

        public void RunBeforeHandle( Action<IDbProvider, TEvent> beforeExpression )
        {
            BeforeExpression = beforeExpression;
        }

        public void RunAfterHandle( Action<IDbProvider, TEvent> afterExpression )
        {
            AfterExpression = afterExpression;
        }

        public void PostProcess( Action<TEvent> postProcessExpression )
        {
            PostProcessExpression = postProcessExpression;
        }

        #endregion
    }
}
