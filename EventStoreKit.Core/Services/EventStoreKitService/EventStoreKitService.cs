using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommonDomain;
using CommonDomain.Core;
using CommonDomain.Persistence;
using CommonDomain.Persistence.EventStore;
using EventStoreKit.Core.EventSubscribers;
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
    public class EventStoreKitService : IEventStoreKitService, IEventStoreKitServiceBuilder
    {
        #region Private fields

        private IEventDispatcher Dispatcher;
        private IEventPublisher EventPublisher;
        private ICommandBus CommandBus;
        private IStoreEvents StoreEvents;
        private IConstructAggregates ConstructAggregates;
        private ICurrentUserProvider CurrentUserProvider;
        private IIdGenerator IdGenerator;
        
        private readonly Dictionary<Type, Func<IEventSubscriber>> EventSubscribers = new Dictionary<Type, Func<IEventSubscriber>>();
        private readonly List<Func<ICommandHandler>> CommandHandlers = new List<Func<ICommandHandler>>();
        private readonly List<IServiceProperty> ServiceProperties = new List<IServiceProperty>();

        private bool Initialized;

        #endregion

        #region Private methods

        private EventStoreKitService ReInitialize()
        {
            if( Initialized )
                Initialize();
            return this;
        }

        private void InitializeCommon()
        {
            IdGenerator = new SequentialIdgenerator();
            CurrentUserProvider = new CurrentUserProviderStub { CurrentUserId = Guid.NewGuid() };

            // initialize properties
            ServiceProperties.ForEach( property => property.Initialize() );

            var dispatcher = new MessageDispatcher( LoggerFactory.Value.Create<MessageDispatcher>() );
            Dispatcher = dispatcher;
            EventPublisher = dispatcher;
            CommandBus = dispatcher;

            // register command handlers
            CommandHandlers.ForEach( ConfigureCommandHandlerRouts );

            // register subscribers
            EventSubscribers.Values.ToList().ForEach( ConfigureEventSubscriberRouts );
        }

        private void InitializeEventStore()
        {
            StoreEvents?.Dispose();
            
            var wireup = InitializeWireup();
            StoreEvents = new EventStoreAdapter( wireup, LoggerFactory.Value.Create<EventStoreAdapter>(), EventPublisher, CommandBus );
            ConstructAggregates = new EntityFactory();
            // todo: register also SagaFactory
        }
        
        private Wireup InitializeWireup()
        {
            var wireup = Wireup
                .Init()
                .LogTo( type => LoggerFactory.Value.Create<EventStoreAdapter>() );

            if ( DbProviderFactoryEventStore == null || DbProviderFactoryEventStore.Value.DefaultDataBaseConfiguration.DataBaseConnectionType == DataBaseConnectionType.None )
            {
                return wireup.UsingInMemoryPersistence();
            }
            else
            {
                var configuration = DbProviderFactoryEventStore.Value.DefaultDataBaseConfiguration;
                var persistanceWireup =
                    configuration.ConfigurationString != null ?
                    wireup.UsingSqlPersistence(configuration.ConfigurationString ) :
                    wireup.UsingSqlPersistence( null, configuration.ConnectionProviderName, configuration.ConnectionString );

                // todo: move NEventStore related stuff to separate module
                var dialectTypeMap = new Dictionary< DataBaseConnectionType, Type>
                {
                    { DataBaseConnectionType.MsSql2000, typeof( MsSqlDialect ) },
                    { DataBaseConnectionType.MsSql2005, typeof( MsSqlDialect ) },
                    { DataBaseConnectionType.MsSql2008, typeof( MsSqlDialect ) },
                    { DataBaseConnectionType.MsSql2012, typeof( MsSqlDialect ) },
                    { DataBaseConnectionType.MySql, typeof( MySqlDialect ) },
                    { DataBaseConnectionType.SqlLite, typeof( SqliteDialect ) }
                };
                return persistanceWireup
                    .WithDialect( (ISqlDialect)Activator.CreateInstance( dialectTypeMap[configuration.DataBaseConnectionType] ) )
                    .PageEvery( 1024 )
                    .InitializeStorageEngine()
                    .UsingJsonSerialization();
            }
        }
        
        private void ConfigureCommandHandlerRouts( Func<ICommandHandler> handlerFactory )
        {
            var handlerType = handlerFactory().GetType();
            var commandHandlerInterfaceType = typeof( ICommandHandler<,> );
            var registerCommandMehod = GetType().GetMethod( "RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance );
            var adjustFactoryTypeMehod = typeof( DelegateAdjuster ).GetMethod( "CastResultToDerived", BindingFlags.Public | BindingFlags.Static );

            handlerType
                .GetInterfaces()
                .Where( h => h.Name == commandHandlerInterfaceType.Name )
                .ToList()
                .ForEach( h =>
                {
                    // ReSharper disable PossibleNullReferenceException
                    var genericArgs = h.GetGenericArguments();
                    var factory = adjustFactoryTypeMehod
                        .MakeGenericMethod( typeof( ICommandHandler ), h )
                        .Invoke( this, new object[] { handlerFactory } );
                    registerCommandMehod
                        .MakeGenericMethod( genericArgs[0], genericArgs[1] )
                        .Invoke( this, new[] { factory } );
                    // ReSharper restore PossibleNullReferenceException
                } );
        }
        private void RegisterCommandHandler<TCommand, TEntity>( Func<ICommandHandler<TCommand, TEntity>> handlerFactory )
            where TCommand : DomainCommand
            where TEntity : class, IAggregate
        {
            // register Action as handler to dispatcher
            var repositoryFactory = new Func<IRepository>( () => new EventStoreRepository( StoreEvents, ConstructAggregates, new ConflictDetector() ) );

            var handleAction = new Action<TCommand>( cmd =>
            {
                var repository = repositoryFactory();
                var handler = handlerFactory();
                var logger = LoggerFactory.Value.Create<EventStoreKitService>();

                if ( cmd.Created == default( DateTime ) )
                    cmd.Created = DateTime.Now;
                if ( cmd.CreatedBy == Guid.Empty && CurrentUserProvider.CurrentUserId != null )
                    cmd.CreatedBy = CurrentUserProvider.CurrentUserId.Value;
                var context = new CommandHandlerContext<TEntity>( () => repository.GetById<TEntity>( cmd.Id ) );

                handler.Handle( cmd, context );
                logger.Info( "{0} processed; version = {1}", cmd.GetType().Name, cmd.Version );

                context.Entity
                    .With( entity => entity.GetUncommittedEvents() )
                    .Do( messages => messages.OfType<Message>().ToList().ForEach( 
                        message =>
                        {
                            message.Created = cmd.Created;
                            message.CreatedBy = cmd.CreatedBy;
                        } ) );

                repository.Save( context.Entity, IdGenerator.NewGuid() );
            } );
            Dispatcher.RegisterHandler( handleAction );
        }

        private TTarget TryCreateInstance<TTarget>( Dictionary<Type, object> arguments )
        {
            return TryCreateInstance( typeof(TTarget), arguments ).OfType<TTarget>();
        }
        private object TryCreateInstance( Type targetType, Dictionary<Type, object> arguments )
        {
            var ctor = targetType
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
            return ctor.With( c => c.Invoke( arguments.Values.ToArray() ) );
        }
        private IDbProviderFactory InitializeDbProviderFactory( Type factoryType, IDataBaseConfiguration config )
        {
            var factory =
                TryCreateInstance( factoryType, new Dictionary<Type, object> { { typeof( IDataBaseConfiguration ), config } } ) ??
                TryCreateInstance( factoryType, new Dictionary<Type, object> { { typeof( string ), config.ConfigurationString } } ) ??
                TryCreateInstance( factoryType, new Dictionary<Type, object> { { typeof( DataBaseConnectionType ), config.DataBaseConnectionType }, { typeof( string ), config.ConnectionString } } ) ??
                TryCreateInstance( factoryType, new Dictionary<Type, object> () );
            if( factory == null )
                throw new InvalidOperationException( $"Can't create {factoryType.Name} instance, because there is no appropriate constructor" );

            return factory.OfType<IDbProviderFactory>();
        }

        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber>( IDataBaseConfiguration config = null ) where TSubscriber : class, IEventSubscriber
        {
            var dbFactory = config.Return(
                c => InitializeDbProviderFactory( DbProviderFactorySubscriber.GetValueOrDefault().GetType(), config ),
                DbProviderFactorySubscriber.GetValueOrDefault() );
            return CreateEventSubscriberContext<TSubscriber>( dbFactory );
        }
        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber>( IDbProviderFactory dbProviderFactory ) where TSubscriber : class, IEventSubscriber
        {
            
            return new EventStoreSubscriberContext
            ( 
                Configuration.GetValueOrDefault(),
                LoggerFactory.GetValueOrDefault(),
                Scheduler.GetValueOrDefault(),
                dbProviderFactory
            );
        }
        private TSubscriber InitializeEventSubscriber<TSubscriber>( IEventStoreSubscriberContext context ) where TSubscriber : class, IEventSubscriber
        {
            var subscriber =
                TryCreateInstance<TSubscriber>( new Dictionary<Type, object>{ { typeof( IEventStoreSubscriberContext ), context } } ) ??
                TryCreateInstance<TSubscriber>( new Dictionary<Type, object>() );

            if( subscriber  == null )
                throw new InvalidOperationException( $"Can't create {typeof( TSubscriber ).Name} instance, because there is no public constructore" );

            return subscriber;
        }

        private void ConfigureEventSubscriberRouts( Func<IEventSubscriber> subscriberFactory )
        {
            var dispatcherType = Dispatcher.GetType();
            var subscriberInstance = subscriberFactory();
            foreach( var handledEventType in subscriberInstance.HandledEventTypes )
            {
                var registerMethod = dispatcherType.GetMethod( "RegisterHandler" ).MakeGenericMethod( handledEventType );
                var handleDelegate = new Action<Message>( message =>
                {
                    var subscriber = subscriberFactory();
                    subscriber.Handle( message );
                } );
                registerMethod.Invoke( Dispatcher, new object[] { handleDelegate } );
            }
        }
        private void RegisterEventSubscriberFactory( Func<IEventSubscriber> subscriberFactory )
        {
            var subscriberInstance = subscriberFactory();
            var subscriberType = subscriberInstance.GetType();
            var basicType = typeof(IEventSubscriber);

            // register as self
            EventSubscribers.Add( subscriberType, subscriberFactory );

            // register as all user-implemented interfaces, which is assignable to IEventSubscriber
            subscriberType
                .GetInterfaces()
                .Where( type => basicType.IsAssignableFrom( type ) && type.Assembly != basicType.Assembly ) // exclude all basic types
                .ToList()
                .ForEach( type =>
                {
                    if ( EventSubscribers.ContainsKey( type ) )
                        throw new InvalidOperationException( $"{type.Name} already registered" );
                    EventSubscribers.Add( type, subscriberFactory );
                } );

            if ( Initialized )
                ConfigureEventSubscriberRouts( subscriberFactory );
        }

        private ServiceProperty<TPropertyValue> InitializeProperty<TPropertyValue>( Func<TPropertyValue> defaultInitializer ) where TPropertyValue : class
        {
            var property = new ServiceProperty<TPropertyValue>( defaultInitializer );
            ServiceProperties.Add( property );
            return property;
        }

        #endregion
        
        public EventStoreKitService( bool initialize = true )
        {
            Configuration = InitializeProperty<IEventStoreConfiguration>( 
                () => new EventStoreConfiguration
                {
                    InsertBufferSize = 10000,
                    OnIddleInterval = 500
                } );
            LoggerFactory = InitializeProperty<ILoggerFactory>( () => new LoggerFactoryStub() );
            Scheduler = InitializeProperty<IScheduler>( () => new NewThreadScheduler( action => new Thread( action ) { IsBackground = true } ) ); // todo: repace with another scheduler
            DbProviderFactorySubscriber = InitializeProperty<IDbProviderFactory>( () => new DbProviderFactoryStub() );
            DbProviderFactoryEventStore = InitializeProperty<IDbProviderFactory>( () => new DbProviderFactoryStub() );
            
            if ( initialize )
                Initialize();
        }
       
        #region IEventStoreKitServiceBuilder implementation
        
        public ServiceProperty<IEventStoreConfiguration> Configuration { get; }
        public ServiceProperty<ILoggerFactory> LoggerFactory { get; }
        public ServiceProperty<IScheduler> Scheduler { get; }
        public ServiceProperty<IDbProviderFactory> DbProviderFactorySubscriber { get; }
        public ServiceProperty<IDbProviderFactory> DbProviderFactoryEventStore { get; }

        public IEventStoreKitServiceBuilder SetConfiguration( IEventStoreConfiguration configuration )
        {
            Configuration.Value = configuration;
            return this;
        }
        public IEventStoreKitServiceBuilder SetLoggerFactory( ILoggerFactory factory )
        {
            LoggerFactory.Value = factory;
            return this;
        }
        public IEventStoreKitServiceBuilder SetScheduler( IScheduler scheduler )
        {
            Scheduler.Value = scheduler;
            return this;
        }

        #region DataBase configuring methods

        public IEventStoreKitServiceBuilder SetDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
            where TDbProviderFactory : IDbProviderFactory
        {
            return SetDataBase( typeof( TDbProviderFactory ), configuration );
        }
        public IEventStoreKitServiceBuilder SetDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration )
        {
            SetSubscriberDataBase( dbProviderFactoryType, configuration );
            SetEventStoreDataBase( dbProviderFactoryType, configuration );
            return this;
        }
        public IEventStoreKitServiceBuilder SetDataBase( IDbProviderFactory factory )
        {
            SetSubscriberDataBase( factory );
            SetEventStoreDataBase( factory );
            return this;
        }


        public IEventStoreKitServiceBuilder SetSubscriberDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
        {
            return SetSubscriberDataBase( typeof( TDbProviderFactory ), configuration );
        }
        public IEventStoreKitServiceBuilder SetSubscriberDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration )
        {
            return SetSubscriberDataBase( InitializeDbProviderFactory( dbProviderFactoryType, configuration ) );
        }
        public IEventStoreKitServiceBuilder SetSubscriberDataBase( IDbProviderFactory factory )
        {
            DbProviderFactorySubscriber.Value = factory;
            return this;
        }

        public IEventStoreKitServiceBuilder SetEventStoreDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
        {
            return SetEventStoreDataBase( typeof( TDbProviderFactory ), configuration );
        }
        public IEventStoreKitServiceBuilder SetEventStoreDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration )
        {
            return SetEventStoreDataBase( InitializeDbProviderFactory( dbProviderFactoryType, configuration ) );
        }
        public IEventStoreKitServiceBuilder SetEventStoreDataBase( IDbProviderFactory factory )
        {
            DbProviderFactoryEventStore.Value = factory;
            ReInitialize();
            return this;
        }

        #endregion

        #region Event Subscribers methods

        public IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory )
            where TSubscriber : class, IEventSubscriber
        {
            RegisterEventSubscriberFactory( () => subscriberFactory( CreateEventSubscriberContext<TSubscriber>() ) );
            return this;
        }
        public IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory, IDataBaseConfiguration configuration )
            where TSubscriber : class, IEventSubscriber
        {
            RegisterEventSubscriberFactory( () => subscriberFactory( CreateEventSubscriberContext<TSubscriber>( configuration ) ) );
            return this;
        }
        public IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory, IDbProviderFactory dbProviderFactory )
            where TSubscriber : class, IEventSubscriber
        {
            RegisterEventSubscriberFactory( () => subscriberFactory( CreateEventSubscriberContext<TSubscriber>( dbProviderFactory ) ) );
            return this;
        }
        public IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( IDataBaseConfiguration configuration ) where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( configuration );
            var subscriber = InitializeEventSubscriber<TSubscriber>( context );
            RegisterEventSubscriberFactory( () => subscriber );
            return this;
        }
        public IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( IDbProviderFactory dbProviderFactory ) where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( dbProviderFactory );
            var subscriber = InitializeEventSubscriber<TSubscriber>( context );
            RegisterEventSubscriberFactory( () => subscriber );
            return this;
        }

        public IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>()
            where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>();
            var subscriber = InitializeEventSubscriber<TSubscriber>( context );
            RegisterEventSubscriberFactory( () => subscriber );
            return this;
        }
        public IEventStoreKitServiceBuilder RegisterEventSubscriber( Func<IEventSubscriber> subscriberFactory )
        {
            RegisterEventSubscriberFactory( subscriberFactory );
            return this;
        }

        public Dictionary<Type, Func<IEventSubscriber>> GetEventSubscribers()
        {
            return EventSubscribers.ToDictionary( s => s.Key, s => s.Value );
        }

        #endregion

        #region Register command handlers methods

        public IEventStoreKitServiceBuilder RegisterCommandHandler<THandler>() where THandler : class, ICommandHandler, new()
        {
            return RegisterCommandHandler( () => new THandler() );
        }
        
        public IEventStoreKitServiceBuilder RegisterCommandHandler( Func<ICommandHandler> handlerFactory )
        {
            CommandHandlers.Add( handlerFactory );

            if ( Initialized )
                ConfigureCommandHandlerRouts( handlerFactory );

            return this;
        }

        #endregion

        public IEventStoreKitService Initialize()
        {
            if( !Initialized )
            {
                InitializeCommon();
            }
            InitializeEventStore();
            Initialized = true;

            return this;
        }

        #endregion

        #region IEventStoreKitService implementation

        public TSubscriber GetSubscriber<TSubscriber>() where TSubscriber : IEventSubscriber
        {
            return (TSubscriber) EventSubscribers[typeof(TSubscriber)]();
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
            var markerMessage = new SequenceMarkerEvent{ Identity = Guid.NewGuid() };
            var targets =
                subscribers.Any() ?
                subscribers.ToList() :
                EventSubscribers.Values
                    .Select( factory => factory() )
                    .Distinct()
                    .ToList();

            var tasks = targets
                .Select( subscriber => subscriber.When<SequenceMarkerEvent>( msg => msg.Identity == markerMessage.Identity ) )
                .ToArray();
            targets.ForEach( subscriber => subscriber.Handle( markerMessage ) );
            Task.WaitAll( tasks );
        }

        public void CleanData()
        {
            StoreEvents.Advanced.Purge();

            var msg = new SystemCleanedUpEvent();
            EventSubscribers
                .Values.ToList()
                .Select( subscriberFactory => subscriberFactory() )
                .ToList()
                .ForEach( s => { s.Handle( msg ); } );
            Wait();
        }

        #endregion

        public void Dispose()
        {
            StoreEvents?.Dispose();
        }
    }
}