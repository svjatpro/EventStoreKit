﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using CommonDomain.Core;
using CommonDomain.Persistence;
using CommonDomain.Persistence.EventStore;
using EventStoreKit.Aggregates;
using EventStoreKit.CommandBus;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Services.IdGenerators;
using EventStoreKit.Utility;
using NEventStore;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;

namespace EventStoreKit.Services
{
    public interface IEventStoreKitService
    {
        TSubscriber ResolveSubscriber<TSubscriber>() where TSubscriber : IEventSubscriber;
        IDbProviderFactory ResolveDbProviderFactory<TModel>();

        void SendCommand( DomainCommand command );
    }


    public class EventStoreKitService : IEventStoreKitService
    {
        #region Private fields

        private IEventDispatcher Dispatcher;
        private IEventPublisher EventPublisher;
        private ICommandBus CommandBus;
        private IStoreEvents StoreEvents;
        private IConstructAggregates ConstructAggregates;
        private ICurrentUserProvider CurrentUserProvider;
        private IIdGenerator IdGenerator;
        private IScheduler Scheduler;
        private IEventStoreConfiguration Configuration;
        
        private readonly Dictionary<Type, IEventSubscriber> EventSubscribers = new Dictionary<Type, IEventSubscriber>();

        private IDataBaseConfiguration DbConfigurationDefault = null;
        private IDataBaseConfiguration DbConfigurationEventStore = null;
        private readonly Dictionary<Type, IDbProviderFactory> DbProviderFactoryMap = new Dictionary<Type, IDbProviderFactory>();
        private readonly Dictionary<IDataBaseConfiguration, IDbProviderFactory> DbProviderFactoryHash = new Dictionary<IDataBaseConfiguration, IDbProviderFactory>();

        #endregion

        #region Private methods

        private void InitizlizeCommon()
        {
            DbConfigurationDefault = DataBaseConfiguration.Initialize( typeof( DbProviderFactoryStub ), string.Empty );
            DbConfigurationEventStore = DbConfigurationDefault;
            DbProviderFactoryHash.Add( DbConfigurationDefault, new DbProviderFactoryStub() );

            IdGenerator = new SequentialIdgenerator();
            Scheduler = new NewThreadScheduler( action => new Thread(action) { IsBackground = true } ); // todo:
            Configuration = new EventStoreConfiguration
            {
                InsertBufferSize = 10000,
                OnIddleInterval = 500
            };

            CurrentUserProvider = new CurrentUserProviderStub { CurrentUserId = Guid.NewGuid() };
        }
        private void InitializeEventStore()
        {
            var dispatcher = new MessageDispatcher( ResolveLogger<MessageDispatcher>() );
            Dispatcher = dispatcher;
            EventPublisher = dispatcher;
            CommandBus = dispatcher;

            var wireup = InitializeWireup();

            StoreEvents = new EventStoreAdapter( wireup, ResolveLogger<EventStoreAdapter>(), EventPublisher, CommandBus );
            ConstructAggregates = new EntityFactory();
            // todo: register also SagaFactory
        }

        private Wireup InitializeWireup()
        {
            var wireup = Wireup
                .Init()
                .LogTo( type => ResolveLogger<EventStoreAdapter>() );

            if ( DbConfigurationEventStore == null )
            {
                return wireup.UsingInMemoryPersistence();
            }
            else
            {
                var persistanceWireup =
                    DbConfigurationEventStore.ConfigurationString != null ?
                    wireup.UsingSqlPersistence( DbConfigurationEventStore.ConfigurationString ) :
                    wireup.UsingSqlPersistence( null, DbConfigurationEventStore.ConnectionProviderName, DbConfigurationEventStore.ConnectionString );

                // todo: move NEventStore related stuff to separate module
                var dialectTypeMap = new Dictionary< DbConnectionType, Type>
                {
                    { DbConnectionType.MsSql, typeof( MsSqlDialect ) },
                    { DbConnectionType.MySql, typeof( MySqlDialect ) },
                    { DbConnectionType.SqlLite, typeof( SqliteDialect ) }
                };
                return persistanceWireup
                    .WithDialect( (ISqlDialect)Activator.CreateInstance( dialectTypeMap[DbConfigurationEventStore.DbConnectionType] ) )
                    .PageEvery( 1024 )
                    .InitializeStorageEngine()
                    .UsingJsonSerialization();
            }
        }

        private void RegisterCommandHandler<TCommand, TEntity>( ICommandHandler<TCommand, TEntity> handlerFactory )
            where TCommand : DomainCommand
            where TEntity : class, ITrackableAggregate
        {
            // register Action as handler to dispatcher
            var logger = ResolveLogger<EventStoreKitService>();
            var repositoryFactory = new Func<IRepository>( ResolveRepository );

            var handleAction = new Action<TCommand>( cmd =>
            {
                var repository = repositoryFactory();
                var handler = handlerFactory;

                if ( cmd.Created == default( DateTime ) )
                    cmd.Created = DateTime.Now;
                if ( cmd.CreatedBy == Guid.Empty && CurrentUserProvider.CurrentUserId != null )
                    cmd.CreatedBy = CurrentUserProvider.CurrentUserId.Value;
                var context = new CommandHandlerContext<TEntity>
                {
                    Entity = repository.GetById<TEntity>( cmd.Id )
                };
                if ( cmd.CreatedBy != Guid.Empty )
                    context.Entity.IssuedBy = cmd.CreatedBy;
                else
                    CurrentUserProvider.CurrentUserId.Do( userId => context.Entity.IssuedBy = userId.GetValueOrDefault() );

                handler.Handle( cmd, context );
                logger.Info( "{0} processed; version = {1}", cmd.GetType().Name, cmd.Version );
                repository.Save( context.Entity, IdGenerator.NewGuid() );
            } );
            Dispatcher.RegisterHandler( handleAction );
        }

        private IDbProviderFactory TryInitializeDbProvider( Type factoryType, Dictionary<Type, object> arguments )
        {
            var ctor = factoryType
                .GetConstructors( BindingFlags.Public | BindingFlags.Instance )
                .FirstOrDefault( c =>
                {
                    var args = c.GetParameters();
                    if( args.Length != arguments.Count )
                        return false;
                    var types = arguments.Keys.ToList();
                    for( var i = 0; i < types.Count; i++ )
                    {
                        if( args[i].ParameterType != types[i] )
                            return false;
                    }
                    return true;
                } );
            return ctor.With( c => c.Invoke( arguments.Values.ToArray() ).OfType<IDbProviderFactory>() );
        }
        private IDbProviderFactory InitializeDbProviderFactory( IDataBaseConfiguration config )
        {
            if ( DbProviderFactoryHash.ContainsKey( config ) )
                return DbProviderFactoryHash[config];

            var factory =
                TryInitializeDbProvider( config.DbProviderFactoryType, new Dictionary<Type, object> { { typeof( IDataBaseConfiguration ), config } } ) ??
                TryInitializeDbProvider( config.DbProviderFactoryType, new Dictionary<Type, object> { { typeof( string ), config.ConfigurationString } } ) ??
                TryInitializeDbProvider( config.DbProviderFactoryType, new Dictionary<Type, object> { { typeof( DbConnectionType ), config.DbConnectionType }, { typeof( string ), config.ConnectionString } } );
            if( factory == null )
                throw new InvalidOperationException( $"Can't create {config.DbProviderFactoryType.Name} instance, because there is no appropriate constructor" );

            DbProviderFactoryHash.Add( config, factory );

            if( DbConfigurationDefault == null )
                DbConfigurationDefault = config;
            if ( DbConfigurationEventStore == null )
                DbConfigurationEventStore = config;

            return factory;
        }

        private IDataBaseConfiguration CreateDbConfig<TDbProviderFactory>( string configurationString )
        {
            return DataBaseConfiguration.Initialize( typeof(TDbProviderFactory), configurationString );
        }
        private IDataBaseConfiguration CreateDbConfig<TDbProviderFactory>( DbConnectionType dbConnection, string connectionString )
        {
            return DataBaseConfiguration.Initialize( typeof( TDbProviderFactory ), dbConnection, connectionString );
        }
        private IDataBaseConfiguration CreateDbConfig( string configurationString )
        {
            return DataBaseConfiguration.Initialize( DbConfigurationDefault.DbProviderFactoryType, configurationString );
        }
        private IDataBaseConfiguration CreateDbConfig( DbConnectionType dbConnection, string connectionString )
        {
            return DataBaseConfiguration.Initialize( DbConfigurationDefault.DbProviderFactoryType, dbConnection, connectionString );
        }

        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber>( string configurationString ) 
            where TSubscriber : class, IEventSubscriber
        {
            return CreateEventSubscriberContext<TSubscriber>( CreateDbConfig( configurationString ) );
        }
        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber,TDbProviderFactory>( string configurationString ) 
            where TSubscriber : class, IEventSubscriber
            where TDbProviderFactory : IDbProviderFactory
        {
            return CreateEventSubscriberContext<TSubscriber>( CreateDbConfig<TDbProviderFactory>( configurationString ) );
        }
        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber>( DbConnectionType connectionType, string connectionString ) 
            where TSubscriber : class, IEventSubscriber
        {
            return CreateEventSubscriberContext<TSubscriber>( CreateDbConfig( connectionType, connectionString ) );
        }
        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber, TDbProviderFactory>( DbConnectionType connectionType, string connectionString ) 
            where TSubscriber : class, IEventSubscriber
            where TDbProviderFactory : IDbProviderFactory
        {
            return CreateEventSubscriberContext<TSubscriber>( CreateDbConfig<TDbProviderFactory>( connectionType, connectionString ) );
        }
        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber>( IDataBaseConfiguration config = null ) where TSubscriber : class, IEventSubscriber
        {
            return new EventStoreSubscriberContext
            {
                Logger = ResolveLogger<TSubscriber>(),
                Scheduler = Scheduler,
                Configuration = Configuration,
                DbProviderFactory = InitializeDbProviderFactory( config ?? DbConfigurationDefault )
            };
        }

        private TSubscriber InitializeEventSubscriber<TSubscriber>( IEventStoreSubscriberContext context ) where TSubscriber : class, IEventSubscriber
        {
            var stype = typeof( TSubscriber );

            var ctor = stype
                .GetConstructors( BindingFlags.Public | BindingFlags.Instance )
                .FirstOrDefault( c =>
                {
                    var args = c.GetParameters();
                    return
                        args.Length == 1 &&
                        args[0].ParameterType == typeof( IEventStoreSubscriberContext );
                } );
            if( ctor == null )
                throw new InvalidOperationException( $"Can't create {stype.Name} instance, because there is no public constructore" );

            return (TSubscriber)ctor.Invoke( new object[] { context } );
        }

        private void InitializeEventStoreDb( IDataBaseConfiguration config )
        {
            DbConfigurationEventStore = config;
            var factory = InitializeDbProviderFactory( config );

            MapReadModelToDbFactory<Commits>( factory, false );
            //MapReadModelToDbFactory<Snapshots>( factory );

            InitializeEventStore();
        }

        private void MapReadModelToDbFactory<TReadModel>( IDbProviderFactory factory, bool unique )
        {
            MapReadModelToDbFactory( typeof(TReadModel), factory, unique );
        }
        private void MapReadModelToDbFactory( Type readModelType, IDbProviderFactory factory, bool unique )
        {
            if ( unique || !DbProviderFactoryMap.ContainsKey( readModelType ) )
            {
                DbProviderFactoryMap.Add( readModelType, factory );
            }
            else
            {
                DbProviderFactoryMap[readModelType] = factory;
            }
        }

        #endregion

        #region Protected methods

        protected virtual ICurrentUserProvider ResolveCurrentUserProvider() { return CurrentUserProvider; }
        protected virtual ILogger<T> ResolveLogger<T>() { return new LoggerStub<T>(); }
        protected virtual IRepository ResolveRepository()
        {
            return new EventStoreRepository(StoreEvents, ConstructAggregates, new ConflictDetector());
        }

        #endregion

        #region Event Subscribers methods
        
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory )
            where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>();
            RegisterEventSubscriber<TSubscriber>( subscriberFactory( context ), context );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory, string configurationString )
            where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( configurationString );
            RegisterEventSubscriber<TSubscriber>( subscriberFactory( context ), context );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory, DbConnectionType dbConnection, string connectionString )
            where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( dbConnection, connectionString );
            RegisterEventSubscriber<TSubscriber>( subscriberFactory( context ), context );
            return this;
        }

        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( string configurationString ) where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( CreateDbConfig( configurationString ) );
            RegisterEventSubscriber<TSubscriber>( InitializeEventSubscriber<TSubscriber>( context ), context );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( DbConnectionType dbConnection, string connectionString ) where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( CreateDbConfig( dbConnection, connectionString ) );
            RegisterEventSubscriber<TSubscriber>( InitializeEventSubscriber<TSubscriber>( context ), context );
            return this;
        }

        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( IEventSubscriber subscriber = null )
            where TSubscriber : class, IEventSubscriber
        {
            IEventStoreSubscriberContext context = null;
            if ( subscriber == null )
            {
                context = CreateEventSubscriberContext<TSubscriber>();
                subscriber = InitializeEventSubscriber<TSubscriber>( context );
            }

            RegisterEventSubscriber<TSubscriber>( subscriber, context );
            return this;
        }
        private void RegisterEventSubscriber<TSubscriber>( IEventSubscriber subscriber, IEventStoreSubscriberContext context = null )
            where TSubscriber : class, IEventSubscriber
        {
            var dispatcherType = Dispatcher.GetType();
            var subscriberType = typeof( IEventSubscriber );
            foreach( var handledEventType in subscriber.HandledEventTypes )
            {
                var registerMethod = dispatcherType.GetMethod( "RegisterHandler" ).MakeGenericMethod( handledEventType );
                var handleMethod = subscriberType.GetMethods().Single( m => m.Name == "Handle" );
                var handleDelegate = Delegate.CreateDelegate( typeof( Action<Message> ), subscriber, handleMethod );
                registerMethod.Invoke( Dispatcher, new object[] { handleDelegate } );
            }

            EventSubscribers.Add( typeof( TSubscriber ), subscriber );

            if( context != null )
            {
                subscriber
                    .OfType<IReadModelOwner>()
                    .Do( s => s.GetReadModels
                        .ForEach( model => MapReadModelToDbFactory( model, context.DbProviderFactory, true ) ) );
            }
        }

        #endregion

        #region Initialize default DbProviderFactory

        /// <summary>
        /// Register default DbProviderFactory
        /// </summary>
        public EventStoreKitService RegisterDbProviderFactory<TDbProviderFactory>( string configurationString )
            where TDbProviderFactory : IDbProviderFactory
        {
            DbConfigurationDefault = CreateDbConfig<TDbProviderFactory>( configurationString );
            DbConfigurationEventStore = DbConfigurationDefault;

            InitializeDbProviderFactory( DbConfigurationDefault );
            InitializeEventStore();

            return this;
        }
        
        /// <summary>
        /// Register default DbProviderFactory
        /// </summary>
        public EventStoreKitService RegisterDbProviderFactory<TDbProviderFactory>( DbConnectionType dbConnection, string connectionString )
            where TDbProviderFactory : IDbProviderFactory
        {
            DbConfigurationDefault = CreateDbConfig<TDbProviderFactory>( dbConnection, connectionString );
            DbConfigurationEventStore = DbConfigurationDefault;

            InitializeDbProviderFactory( DbConfigurationDefault );
            InitializeEventStore();

            return this;
        }

        #endregion

        #region Initialize EventStore DataBase
        
        public EventStoreKitService MapEventStoreDb( string configurationString )
        {
            InitializeEventStoreDb( CreateDbConfig( configurationString ) );
            return this;
        }
        public EventStoreKitService MapEventStoreDb<TDbProviderFactory>( string configurationString )
        {
            InitializeEventStoreDb( CreateDbConfig<TDbProviderFactory>( configurationString ) );
            return this;
        }
        public EventStoreKitService MapEventStoreDb( DbConnectionType dbConnection, string connectionString )
        {
            InitializeEventStoreDb( CreateDbConfig( dbConnection, connectionString ) );
            return this;
        }
        public EventStoreKitService MapEventStoreDb<TDbProviderFactory>( DbConnectionType dbConnection, string connectionString )
        {
            InitializeEventStoreDb( CreateDbConfig<TDbProviderFactory>( dbConnection, connectionString ) );
            return this;
        }

        #endregion
        
        public EventStoreKitService()
        {
            InitizlizeCommon();
            InitializeEventStore();
        }

        public EventStoreKitService RegisterCommandHandler<THandler>( THandler handler = null ) where THandler : class, ICommandHandler, new()
        {
            // todo: remove constraints for class and new(), try to create by reflectin, otherwise throw exception
            // but user is able to register subscriber instance, which has no parameterless constructor
            if ( handler == null )
                handler = new THandler();

            var handlerType = typeof(THandler);
            var commandHandlerInterfaceType = typeof(ICommandHandler<,>);
            var registerCommandMehod = GetType().GetMethod( "RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance );

            handlerType
                .GetInterfaces()
                .Where(h => h.Name == commandHandlerInterfaceType.Name)
                .ToList()
                .ForEach(h =>
                {
                    var genericArgs = h.GetGenericArguments();
                    registerCommandMehod
                        .MakeGenericMethod(genericArgs[0], genericArgs[1])
                        .Invoke(this, new object[] { handler });
                });

            return this;
        }
       
        public TSubscriber ResolveSubscriber<TSubscriber>() where TSubscriber : IEventSubscriber
        {
            return (TSubscriber) EventSubscribers[typeof(TSubscriber)];
        }

        public IDbProviderFactory ResolveDbProviderFactory<TModel>()
        {
            var key = typeof(TModel);
            if ( DbProviderFactoryMap.ContainsKey( key ) )
                return DbProviderFactoryMap[key];
            else
                return DbProviderFactoryHash[DbConfigurationDefault];
        }

        public void SendCommand( DomainCommand command )
        {
            CommandBus.Send( command );
        }
    }
}