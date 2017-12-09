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

        private Type DefaultDbProviderType;
        private IDataBaseConfiguration DbConfigurationDefault = null;
        private IDataBaseConfiguration DbConfigurationEventStore = null;
        private IDbProviderFactory DbProviderFactoryDefault;
        private readonly Dictionary<Type, IDbProviderFactory> DbProviderFactoryMap = new Dictionary<Type, IDbProviderFactory>();
        private readonly Dictionary<int, IDbProviderFactory> DbProviderFactoryHash = new Dictionary<int, IDbProviderFactory>();

        #endregion

        #region Private methods

        private void InitizlizeCommon()
        {
            DefaultDbProviderType = typeof(DbProviderFactoryStub);
            DbProviderFactoryDefault = new DbProviderFactoryStub();

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
        private IDbProviderFactory InitializeDbProviderFactory( Type factoryType, IDataBaseConfiguration config )
        {
            var factory =
                TryInitializeDbProvider( factoryType, new Dictionary<Type, object> { { typeof( IDataBaseConfiguration ), config } } ) ??
                TryInitializeDbProvider( factoryType, new Dictionary<Type, object> { { typeof( string ), config.ConfigurationString } } ) ??
                TryInitializeDbProvider( factoryType, new Dictionary<Type, object> { { typeof( DbConnectionType ), config.DbConnectionType }, { typeof( string ), config.ConnectionString } } );
            if( factory == null )
                throw new InvalidOperationException( $"Can't create {DefaultDbProviderType.Name} instance, because there is no appropriate constructor" );

            return factory;
        }

        private IEventStoreSubscriberContext CreateContext<TSubscriber>( string configurationString ) where TSubscriber : class, IEventSubscriber
        {

            return CreateContext<TSubscriber>();
        }
        private IEventStoreSubscriberContext CreateContext<TSubscriber>( DbConnectionType connectionType, string connectionString ) where TSubscriber : class, IEventSubscriber
        {
            
        }
        private IEventStoreSubscriberContext CreateContext<TSubscriber>( IDbProviderFactory factory = null ) where TSubscriber : class, IEventSubscriber
        {
            return new EventStoreSubscriberContext
            {
                Logger = ResolveLogger<TSubscriber>(),
                Scheduler = Scheduler,
                Configuration = Configuration,
                DbProviderFactory = factory ?? DbProviderFactoryDefault
            };
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
            return RegisterEventSubscriber<TSubscriber>( subscriberFactory( CreateContext<TSubscriber>() ) );
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( IEventSubscriber subscriber = null )
            where TSubscriber : class, IEventSubscriber
        {
            if( subscriber == null )
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

                subscriber = (TSubscriber)ctor.Invoke( new object[] { CreateContext<TSubscriber>() } );
            }

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
        
        public EventStoreKitService RegisterDbProviderFactory<TDbProviderFactory>( string configurationString )
            where TDbProviderFactory : IDbProviderFactory
        {
            DbConfigurationDefault = DataBaseConfiguration.Initialize( configurationString );
            DbConfigurationEventStore = DbConfigurationDefault;

            DefaultDbProviderType = typeof(TDbProviderFactory);
            DbProviderFactoryDefault = InitializeDbProviderFactory( DefaultDbProviderType, DbConfigurationDefault );

            InitializeEventStore();

            return this;
        }
        public EventStoreKitService RegisterDbProviderFactory<TDbProviderFactory>( DbConnectionType dbConnection, string connectionString )
            where TDbProviderFactory : IDbProviderFactory
        {
            DbConfigurationDefault = DataBaseConfiguration.Initialize( dbConnection, connectionString );
            DbConfigurationEventStore = DbConfigurationDefault;

            DefaultDbProviderType = typeof(TDbProviderFactory);
            DbProviderFactoryDefault = InitializeDbProviderFactory(DefaultDbProviderType, DbConfigurationDefault);

            InitializeEventStore();

            return this;
        }

        private void MapReadModelToDbFactory<TReadModel>( IDbProviderFactory factory )
        {
            var key = typeof( TReadModel );
            if( !DbProviderFactoryMap.ContainsKey( key ) )
                DbProviderFactoryMap.Add( key, factory );
            else
                DbProviderFactoryMap[key] = factory;
        }
        private void InitDbProviderFactory( Type dbProviderFactoryType, IDataBaseConfiguration dbConfig )
        {
            DbConfigurationEventStore = dbConfig;
            var factory = InitializeDbProviderFactory( dbProviderFactoryType, dbConfig );
            MapReadModelToDbFactory<Commits>( factory );

            InitializeEventStore();
        }
        public EventStoreKitService MapEventStoreDb( string configurationString )
        {
            InitDbProviderFactory( DefaultDbProviderType, DataBaseConfiguration.Initialize( configurationString ) );
            return this;
        }
        public EventStoreKitService MapEventStoreDb<TDbProviderFactory>( string configurationString )
        {
            InitDbProviderFactory( typeof( TDbProviderFactory ), DataBaseConfiguration.Initialize( configurationString ) );
            return this;
        }
        public EventStoreKitService MapEventStoreDb(DbConnectionType dbConnection, string connectionString)
        {
            InitDbProviderFactory( DefaultDbProviderType, DataBaseConfiguration.Initialize( dbConnection, connectionString ) );
            return this;
        }
        public EventStoreKitService MapEventStoreDb<TDbProviderFactory>( DbConnectionType dbConnection, string connectionString )
        {
            InitDbProviderFactory( typeof( TDbProviderFactory ), DataBaseConfiguration.Initialize( dbConnection, connectionString ) );
            return this;
        }

        public EventStoreKitService MapReadModelDb<TReadModel>( string configurationString )
        {
            var dbConfig = DataBaseConfiguration.Initialize( configurationString );
            var factory = InitializeDbProviderFactory( DefaultDbProviderType, dbConfig );
            MapReadModelToDbFactory<TReadModel>( factory );

            return this;
        }
        public EventStoreKitService MapReadModelDb<TReadModel>( DbConnectionType dbConnection, string connectionString )
        {
            var dbConfig = DataBaseConfiguration.Initialize( dbConnection, connectionString );
            var factory = InitializeDbProviderFactory( DefaultDbProviderType, dbConfig );
            MapReadModelToDbFactory<TReadModel>( factory );

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
                return DbProviderFactoryDefault;
        }

        public void SendCommand(DomainCommand command)
        {
            CommandBus.Send( command );
        }
    }
}
