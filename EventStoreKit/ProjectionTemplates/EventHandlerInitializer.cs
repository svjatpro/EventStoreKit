using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EventStoreKit.DbProviders;
using EventStoreKit.Messages;
using EventStoreKit.Utility;

namespace EventStoreKit.ProjectionTemplates
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

        private readonly ConcurrentDictionary<Guid, TReadModel> Cache;
        private readonly HashSet<Type> EventsToFlush = new HashSet<Type>();

        private readonly Action<Type, Action<Message>> EventRegister;
        private readonly Func<IDbProvider> DbFactory;
        private readonly IDbStrategy<TReadModel> DbStrategy;

        private readonly Dictionary<PropertyInfo, EventFieldInfo> PropertiesMap = new Dictionary<PropertyInfo, EventFieldInfo>();
 
        private readonly Dictionary<PropertyInfo, Func<TEvent, object>> ReadModelGetters = new Dictionary<PropertyInfo, Func<TEvent, object>>();

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
            return ReadModelGetters
                .Where( g => g.Key.PropertyType == typeof(Guid) )
                .Select( g => g.Value )
                .FirstOrDefault()
                .Return( get => (Guid)get( @event ), Guid.NewGuid() );
        }
        private Expression<Func<TReadModel, bool>> GetReadModelPredicat( TEvent @event )
        {
            if ( ReadModelGetters.Any() )
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
            return null;
        }

        private void AddToCache( TEvent @event, TReadModel model )
        {
            if( Cache != null )
            {
                var id = GetReadModelId( @event );
                Cache.TryAdd( id, model );
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
        }

        public void Flush()
        {
            DbStrategy.Flush();
        }

        public void CleanUp()
        {
        }
        
        #endregion

        public EventHandlerInitializer(
            Action<Type, Action<Message>, bool> eventRegister, 
            Func<IDbProvider> dbFactory,
            IDbStrategy<TReadModel> dbStrategy,
            ConcurrentDictionary<Guid, TReadModel> cache = null )
        {
            EventRegister = ( type, action ) => eventRegister( type, action, true );
            
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
        /// Initialize handler to Update entity;
        /// </summary>
        public EventHandlerInitializer<TReadModel, TEvent> AsUpdateAction()
        {
            var eventType = typeof( TEvent );

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

                            // run custom action before update
                            BeforeExpression.Do( a => a( db, (TEvent) @event ) );

                            var id = GetReadModelId((TEvent)@event);
                            var predicat = GetReadModelPredicat( (TEvent) @event );
                            var udpateExpr = UpdateExpression.With( getter => getter( db, (TEvent) @event ) );
                            var eventValues = PropertiesMap
                                .Where( p => p.Value.Validator( (TEvent) @event ) )
                                .ToDictionary( p => p.Key, p => p.Value.Getter( db, (TEvent) @event ) );
                            var expresionBuilder = new ObjectExpressionBuilder<TReadModel>( udpateExpr, eventValues );

                            DbStrategy.Update( id, predicat, expresionBuilder );

                            // run custom action after update
                            AfterExpression.Do( a => a( db, (TEvent) @event ) );

                            // update cache
                            if ( predicat != null && Cache != null )
                            {
                                // todo: compile expression and use it to update existing entity without additional DB query
                                var entity = DbStrategy.GetEntity( id, predicat );
                                //var entity = db.Query<TReadModel>().Where( predicat ).FirstOrDefault();
                                if ( entity != null )
                                {
                                    Cache.AddOrUpdate( id, id1 => entity, ( id1, prev ) => entity );
                                }
                            }
                        } );
                    PostProcessExpression.Do( a => a( (TEvent)@event ) );
                } );
            return this;
        }

        /// <summary>
        /// Initialize handler to Delete entity;
        /// </summary>
        public EventHandlerInitializer<TReadModel, TEvent> AsDeleteAction(
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
                                TReadModel m;
                                Cache.TryRemove( id, out m );
                            }

                            // run custom action
                            var action = deleteAction.With( a => a() );
                            if ( action != null )
                                action( db, (TEvent) @event );
                        } );
                    PostProcessExpression.Do( a => a( (TEvent)@event ) );
                } );
            return this;
        }

        #region Customization methods

        public EventHandlerInitializer<TReadModel, TEvent> InitNewEntityWith( Action<IDbProvider, TEvent, TReadModel> initNewEntityExpression )
        {
            InitNewEntityExpression = initNewEntityExpression;
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> UpdateWith( Func<IDbProvider, TEvent, Expression<Func<TReadModel, TReadModel>>> updateExpression )
        {
            UpdateExpression = updateExpression;
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> ValidateWith( Func<IDbProvider, TEvent, bool> validateExpression )
        {
            ValidateExpression = validateExpression;
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> RunBeforeHandle( Action<IDbProvider, TEvent> beforeExpression )
        {
            BeforeExpression = beforeExpression;
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> RunAfterHandle( Action<IDbProvider, TEvent> afterExpression )
        {
            AfterExpression = afterExpression;
            return this;
        }

        public EventHandlerInitializer<TReadModel, TEvent> PostProcess( Action<TEvent> postProcessExpression )
        {
            PostProcessExpression = postProcessExpression;
            return this;
        }

        #endregion
    }
}
