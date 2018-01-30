using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommonDomain.Core;
using CommonDomain.Persistence;
using CommonDomain.Persistence.EventStore;
using EventStoreKit.Aggregates;
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

        private IDataBaseConfiguration DbConfigurationSubscribers = null;
        private IDataBaseConfiguration DbConfigurationEventStore = null;
        private readonly Dictionary<Type, IDbProviderFactory> DbProviderFactoryMap = new Dictionary<Type, IDbProviderFactory>();
        private readonly Dictionary<IDataBaseConfiguration, IDbProviderFactory> DbProviderFactoryHash = new Dictionary<IDataBaseConfiguration, IDbProviderFactory>();

        #endregion

        #region Private methods

        private void InitializeCommon()
        {
            DbConfigurationSubscribers = new DataBaseConfiguration{ DbProviderFactoryType = typeof( DbProviderFactoryStub ), DbConnectionType = DbConnectionType.None, ConnectionString = "stub" };
            DbConfigurationEventStore = DbConfigurationSubscribers;
            DbProviderFactoryHash.Add( DbConfigurationSubscribers, new DbProviderFactoryStub() );

            IdGenerator = new SequentialIdgenerator();
            Scheduler = new NewThreadScheduler( action => new Thread(action) { IsBackground = true } ); // todo:
            Configuration = new EventStoreConfiguration
            {
                InsertBufferSize = 10000,
                OnIddleInterval = 500
            };

            CurrentUserProvider = new CurrentUserProviderStub { CurrentUserId = Guid.NewGuid() };

            var dispatcher = new MessageDispatcher( ResolveLogger<MessageDispatcher>() );
            Dispatcher = dispatcher;
            EventPublisher = dispatcher;
            CommandBus = dispatcher;
        }
        private void InitializeEventStore()
        {
            var factory = DbProviderFactoryHash[DbConfigurationEventStore];
            MapReadModelToDbFactory<Commits>( factory, false );
            //MapReadModelToDbFactory<Snapshots>( factory );

            StoreEvents?.Dispose();
            
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

            if ( DbConfigurationEventStore == null || DbConfigurationEventStore.DbConnectionType == DbConnectionType.None )
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
                    { DbConnectionType.MsSql2000, typeof( MsSqlDialect ) },
                    { DbConnectionType.MsSql2005, typeof( MsSqlDialect ) },
                    { DbConnectionType.MsSql2008, typeof( MsSqlDialect ) },
                    { DbConnectionType.MsSql2012, typeof( MsSqlDialect ) },
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

        private void RegisterCommandHandler<TCommand, TEntity>( Func<ICommandHandler<TCommand, TEntity>> handlerFactory )
            where TCommand : DomainCommand
            where TEntity : class, ITrackableAggregate
        {
            // register Action as handler to dispatcher
            var loggerFactory = new Func<ILogger>( ResolveLogger<EventStoreKitService> );
            var repositoryFactory = new Func<IRepository>( ResolveRepository );

            var handleAction = new Action<TCommand>( cmd =>
            {
                var repository = repositoryFactory();
                var handler = handlerFactory();
                var logger = loggerFactory();

                if ( cmd.Created == default( DateTime ) )
                    cmd.Created = DateTime.Now;
                if ( cmd.CreatedBy == Guid.Empty && CurrentUserProvider.CurrentUserId != null )
                    cmd.CreatedBy = CurrentUserProvider.CurrentUserId.Value;
                var context = new CommandHandlerContext<TEntity>{  Entity = repository.GetById<TEntity>( cmd.Id ) };
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
        private IDataBaseConfiguration CreateDbConfig( Type dbProviderFactoryType, string configurationString )
        {
            return DataBaseConfiguration.Initialize( dbProviderFactoryType, configurationString );
        }
        private IDataBaseConfiguration CreateDbConfig( Type dbProviderFactoryType, DbConnectionType dbConnection, string connectionString )
        {
            return DataBaseConfiguration.Initialize( dbProviderFactoryType, dbConnection, connectionString );
        }

        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber>( string configurationString ) 
            where TSubscriber : class, IEventSubscriber
        {
            return CreateEventSubscriberContext<TSubscriber>( CreateDbConfig( DbConfigurationSubscribers.DbProviderFactoryType, configurationString ) );
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
            return CreateEventSubscriberContext<TSubscriber>( CreateDbConfig( DbConfigurationSubscribers.DbProviderFactoryType, connectionType, connectionString ) );
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
                DbProviderFactory = InitializeDbProviderFactory( config ?? DbConfigurationSubscribers )
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
        private void RegisterEventSubscriber<TSubscriber>( Func<IEventSubscriber> subscriberFactory, IEventStoreSubscriberContext context )
            where TSubscriber : class, IEventSubscriber
        {
            var dispatcherType = Dispatcher.GetType();
            var subscriberType = typeof( IEventSubscriber );
            var subscriber = subscriberFactory();
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

        private void InitializeEventStoreDb( IDataBaseConfiguration config )
        {
            DbConfigurationEventStore = config;
            InitializeDbProviderFactory( config );
            
            InitializeEventStore();
        }

        private void InitializeSubscribersDb( IDataBaseConfiguration config )
        {
            DbConfigurationSubscribers = config;
            InitializeDbProviderFactory( DbConfigurationSubscribers );
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
            var context = CreateEventSubscriberContext<TSubscriber>( configurationString );
            RegisterEventSubscriber<TSubscriber>( InitializeEventSubscriber<TSubscriber>( context ), context );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( DbConnectionType dbConnection, string connectionString ) where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( dbConnection, connectionString );
            RegisterEventSubscriber<TSubscriber>( InitializeEventSubscriber<TSubscriber>( context ), context );
            return this;
        }

        public EventStoreKitService RegisterEventSubscriber<TSubscriber>()
            where TSubscriber : class, IEventSubscriber
        {
            IEventStoreSubscriberContext context = null;
            context = CreateEventSubscriberContext<TSubscriber>();
            var subscriber = InitializeEventSubscriber<TSubscriber>( context );

            RegisterEventSubscriber<TSubscriber>( subscriber, context );
            return this;
        }

        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<TSubscriber> subscriberFactory ) where TSubscriber : class, IEventSubscriber
        {
            //RegisterEventSubscriber<TSubscriber>( subscriber, context );
            return this;
        }


        #endregion

        #region Set Subscriber DataBase

        /// <summary>
        /// Register Subscribers DataBase
        /// </summary>
        public EventStoreKitService SetSubscriberDataBase( string configurationString )
        {
            InitializeSubscribersDb( CreateDbConfig( DbConfigurationSubscribers.DbProviderFactoryType, configurationString ) );
            return this;
        }
        /// <summary>
        /// Register Subscribers DataBase
        /// </summary>
        public EventStoreKitService SetSubscriberDataBase<TDbProviderFactory>( string configurationString ) where TDbProviderFactory : IDbProviderFactory
        {
            InitializeSubscribersDb( CreateDbConfig<TDbProviderFactory>( configurationString ) );
            return this;
        }

        /// <summary>
        /// Register Subscribers DataBase
        /// </summary>
        public EventStoreKitService SetSubscriberDataBase( DbConnectionType dbConnection, string connectionString )
        {
            InitializeSubscribersDb( CreateDbConfig( DbConfigurationSubscribers.DbProviderFactoryType, dbConnection, connectionString ) );
            return this;
        }
        /// <summary>
        /// Register Subscribers DataBase
        /// </summary>
        public EventStoreKitService SetSubscriberDataBase<TDbProviderFactory>( DbConnectionType dbConnection, string connectionString ) where TDbProviderFactory : IDbProviderFactory
        {
            InitializeSubscribersDb( CreateDbConfig<TDbProviderFactory>( dbConnection, connectionString ) );
            return this;
        }

        #endregion

        #region Set EventStore DataBase

        public EventStoreKitService SetEventStoreDataBase( string configurationString )
        {
            InitializeEventStoreDb( CreateDbConfig( DbConfigurationEventStore.DbProviderFactoryType, configurationString ) );
            return this;
        }
        public EventStoreKitService SetEventStoreDataBase<TDbProviderFactory>( string configurationString )
            where TDbProviderFactory : IDbProviderFactory
        {
            InitializeEventStoreDb( CreateDbConfig<TDbProviderFactory>( configurationString ) );
            return this;
        }
        public EventStoreKitService SetEventStoreDataBase( DbConnectionType dbConnection, string connectionString )
        {
            InitializeEventStoreDb( CreateDbConfig( DbConfigurationEventStore.DbProviderFactoryType, dbConnection, connectionString ) );
            return this;
        }
        public EventStoreKitService SetEventStoreDataBase<TDbProviderFactory>( DbConnectionType dbConnection, string connectionString ) 
            where TDbProviderFactory : IDbProviderFactory
        {
            InitializeEventStoreDb( CreateDbConfig<TDbProviderFactory>( dbConnection, connectionString ) );
            return this;
        }

        #endregion
        
        public EventStoreKitService()
        {
            InitializeCommon();
            InitializeEventStore();
        }
        
        public EventStoreKitService RegisterCommandHandler<THandler>() where THandler : class, ICommandHandler, new()
        {
            return RegisterCommandHandler( () => new THandler() );
        }

        public EventStoreKitService RegisterCommandHandler<THandler>( Func<THandler> handlerFactory ) where THandler : ICommandHandler
        {
            var handlerType = handlerFactory().GetType();
            var commandHandlerInterfaceType = typeof(ICommandHandler<,>);
            var registerCommandMehod = GetType().GetMethod( "RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance );
            var adjustFactoryTypeMehod = typeof( DelegateAdjuster ).GetMethod( "CastResultToDerived", BindingFlags.Public | BindingFlags.Static );
            
            handlerType
                .GetInterfaces()
                .Where(h => h.Name == commandHandlerInterfaceType.Name)
                .ToList()
                .ForEach(h =>
                {
// ReSharper disable PossibleNullReferenceException
                    var genericArgs = h.GetGenericArguments();
                    var factory = adjustFactoryTypeMehod
                        .MakeGenericMethod( typeof( ICommandHandler ), h )
                        .Invoke( this, new object[] { handlerFactory } );
                    registerCommandMehod
                        .MakeGenericMethod(genericArgs[0], genericArgs[1])
                        .Invoke(this, new[] { factory } );
// ReSharper restore PossibleNullReferenceException
                } );

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
                return DbProviderFactoryHash[DbConfigurationSubscribers];
        }

        public void SendCommand( DomainCommand command )
        {
            CommandBus.Send( command );
        }

        public void RaiseEvent( DomainEvent message )
        {
            if ( message.CreatedBy == Guid.Empty && CurrentUserProvider.CurrentUserId != null )
                message.CreatedBy = CurrentUserProvider.CurrentUserId.Value;
            if ( message.Created == default(DateTime) || message.Created <= DateTime.MinValue )
                message.Created = DateTime.Now.TrimMilliseconds();

            using ( var stream = StoreEvents.CreateStream( message.Id ) )
            {
                stream.Add( new EventMessage {Body = message} );
                stream.CommitChanges( IdGenerator.NewGuid() );
            }
        }

        public void Publish( DomainEvent message )
        {
            EventPublisher.Publish( message );
        }

        public void Wait( params IEventSubscriber[] subscribers )
        {
            var targets =
                subscribers.Any() ?
                subscribers.ToList() :
                EventSubscribers.Values.ToList();

            var tasks = targets.Select( s => s.WaitMessagesAsync() ).ToArray();
            Task.WaitAll( tasks );
        }

        public void CleanData()
        {
            StoreEvents.Advanced.Purge();

            var msg = new SystemCleanedUpEvent();
            var tasks = EventSubscribers
                .Values.ToList()
                .Select( subscriber =>
                {
                    subscriber.Handle( msg );
                    return subscriber.WaitMessagesAsync();
                } )
                .ToArray();
            Task.WaitAll( tasks );
        }
    }
}
