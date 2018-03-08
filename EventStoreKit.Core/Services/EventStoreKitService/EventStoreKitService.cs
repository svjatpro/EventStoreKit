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
using EventStoreKit.Core.Utility;
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
        private SagaFactory SagasFactory;
        private IIdGenerator IdGenerator;
        private Func<IRepository> RepositoryFactory;
        private Func<ISagaRepository> SagaRepositoryFactory;
        
        private readonly Dictionary<Type, Func<IEventSubscriber>> EventSubscribers = new Dictionary<Type, Func<IEventSubscriber>>();
        private readonly List<SagaRegistrationInfo> SagaRegistration = new List<SagaRegistrationInfo>();
        private readonly List<Type> AggregateCommandHandlers = new List<Type>();
        private readonly List<Func<ICommandHandler>> CommandHandlers = new List<Func<ICommandHandler>>();

        private readonly List<IServiceProperty> ServiceProperties = new List<IServiceProperty>();

        private bool Initialized;

        private class SagaRegistrationInfo
        {
            public Type SagaType { get; set; }
            public Func<string,ISaga> FactoryMethod { get; set; }
            public Dictionary<Type, Func<Message, string>> IdResolvingMap { get; set; }
        }

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

            // initialize properties
            ServiceProperties.ForEach( property => property.Initialize() );

            var dispatcher = new MessageDispatcher( LoggerFactory.Value.Create<MessageDispatcher>() );
            Dispatcher = dispatcher;
            EventPublisher = dispatcher;
            CommandBus = dispatcher;

            ConstructAggregates = new EntityFactory();
            SagasFactory = new SagaFactory();

            // register command handlers
            CommandHandlers.ForEach( ConfigureCommandHandlerRouts );
            AggregateCommandHandlers.ForEach( ConfigureAggregateCommandHandlerRouts );

            // register subscribers
            EventSubscribers.Values.ToList().ForEach( ConfigureEventSubscriberRouts );

            // register sagas
            SagaRegistration.ForEach( ConfigureSagasRouts );
        }

        private void InitializeEventStore()
        {
            StoreEvents?.Dispose();
            
            var wireup = InitializeWireup();
            StoreEvents = new EventStoreAdapter( wireup, LoggerFactory.Value.Create<EventStoreAdapter>(), EventPublisher, CommandBus );
            
            RepositoryFactory = () => new EventStoreRepository( StoreEvents, ConstructAggregates, new ConflictDetector() );
            SagaRepositoryFactory = () => new SagaEventStoreRepository(StoreEvents, SagasFactory );
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

        private void ConfigureSagasRouts( SagaRegistrationInfo sagaRegistration )
        {
            var reg = sagaRegistration;

            // register factory method
            reg.FactoryMethod.Do( factory => SagasFactory.RegisterSagaConstructor( reg.SagaType, factory ) );

            var interfaceType = typeof( IEventHandler<> );
            var dispatcherType = Dispatcher.GetType();
            var getByIdMethod = typeof(ISagaRepository).GetMethod( "GetById" )?.MakeGenericMethod( reg.SagaType );

            reg.SagaType
                .GetInterfaces()
                .Where( handlerIntefrace => handlerIntefrace.IsGenericType && handlerIntefrace.GetGenericTypeDefinition() == interfaceType.GetGenericTypeDefinition() )
                .ToList()
                .ForEach( handlerInterface =>
                {
                    var genericArgs = handlerInterface.GetGenericArguments();
                    
                    var registerMethod = dispatcherType.GetMethod( "RegisterHandler" )?.MakeGenericMethod( genericArgs[0] );
                    var handleDelegate = new Action<Message>( 
                        message =>
                        {
                            var sagaId = reg.IdResolvingMap.GetSagaId( reg.SagaType, message );
                            var sagaRepository = SagaRepositoryFactory();
                            var saga = getByIdMethod?
                                .Invoke( sagaRepository, new []{ Bucket.Default, sagaId } )
                                .OfType<ISaga>();
                            saga?.Transition( message );
                            sagaRepository.Save( saga, Guid.NewGuid(), a => { } );
                        } );
                    registerMethod?.Invoke(Dispatcher, new object[] { handleDelegate });
                });
        }

        private void ConfigureAggregateCommandHandlerRouts( Type aggregateType )
        {
            var commandHandlerInterfaceType = typeof( ICommandHandler<> );
            var registerCommandMehod = GetType().GetMethod( "RegisterAggregateCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance );

            aggregateType
                .GetInterfaces()
                .Where( handlerInterface => handlerInterface.IsGenericType && handlerInterface.GetGenericTypeDefinition() == commandHandlerInterfaceType.GetGenericTypeDefinition() )
                .ToList()
                .ForEach( handlerInterface =>
                {
                    // ReSharper disable PossibleNullReferenceException
                    var genericArgs = handlerInterface.GetGenericArguments();
                    registerCommandMehod
                        .MakeGenericMethod( genericArgs[0], aggregateType )
                        .Invoke( this, new object[] {} );
                    // ReSharper restore PossibleNullReferenceException
                } );
        }

        private void RegisterAggregateCommandHandler<TCommand,TAggregate>()
            where TCommand : DomainCommand
            where TAggregate : class, IAggregate
        {
            // register Action as handler to dispatcher
            var handleAction = new Action<TCommand>( cmd =>
            {
                var repository = RepositoryFactory();
                var aggregate = repository.GetById<TAggregate>( cmd.Id );
                var handler = aggregate.OfType<ICommandHandler<TCommand>>();
                var logger = LoggerFactory.Value.Create<EventStoreKitService>();

                if( cmd.Created == default( DateTime ) )
                    cmd.Created = DateTime.Now;
                if( cmd.CreatedBy == Guid.Empty && CurrentUserProvider.Value.CurrentUserId != null )
                    cmd.CreatedBy = CurrentUserProvider.Value.CurrentUserId.Value;

                handler.Handle( cmd );
                logger.Info( "{0} processed; version = {1}", cmd.GetType().Name, cmd.Version );

                aggregate
                    .With( entity => entity.GetUncommittedEvents() )
                    .Do( messages => messages.OfType<Message>().ToList().ForEach(
                        message =>
                        {
                            message.Created = cmd.Created;
                            message.CreatedBy = cmd.CreatedBy;
                        } ) );

                repository.Save( aggregate, IdGenerator.NewGuid() );
            } );
            Dispatcher.RegisterHandler( handleAction );
        }

        private void ConfigureCommandHandlerRouts( Func<ICommandHandler> handlerFactory )
        {
            var handlerType = handlerFactory().GetType();
            var commandHandlerInterfaceType = typeof( ICommandHandler<,> );
            var registerCommandMehod = GetType().GetMethod( "RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance );
            var adjustFactoryTypeMehod = typeof( DelegateAdjuster ).GetMethod( "CastResultToDerived", BindingFlags.Public | BindingFlags.Static );

            handlerType
                .GetInterfaces()
                .Where( handlerInterface => handlerInterface.IsGenericType && handlerInterface.GetGenericTypeDefinition() == commandHandlerInterfaceType.GetGenericTypeDefinition() ) 
                .ToList()
                .ForEach( handlerInterface =>
                {
                    // ReSharper disable PossibleNullReferenceException
                    var genericArgs = handlerInterface.GetGenericArguments();
                    var factory = adjustFactoryTypeMehod
                        .MakeGenericMethod( typeof( ICommandHandler ), handlerInterface )
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
            var handleAction = new Action<TCommand>( cmd =>
            {
                var repository = RepositoryFactory();
                var handler = handlerFactory();
                var logger = LoggerFactory.Value.Create<EventStoreKitService>();

                if ( cmd.Created == default( DateTime ) )
                    cmd.Created = DateTime.Now;
                if ( cmd.CreatedBy == Guid.Empty && CurrentUserProvider.Value.CurrentUserId != null )
                    cmd.CreatedBy = CurrentUserProvider.Value.CurrentUserId.Value;
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

        private IDbProviderFactory InitializeDbProviderFactory( Type factoryType, IDataBaseConfiguration config )
        {
            var factory =
                factoryType.TryCreateInstance( new Dictionary<Type, object> { { typeof( IDataBaseConfiguration ), config } } ) ??
                factoryType.TryCreateInstance( new Dictionary<Type, object> { { typeof( string ), config.ConfigurationString } } ) ??
                factoryType.TryCreateInstance( new Dictionary<Type, object> { { typeof( DataBaseConnectionType ), config.DataBaseConnectionType }, { typeof( string ), config.ConnectionString } } ) ??
                factoryType.TryCreateInstance( new Dictionary<Type, object>() );
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
            var subscriberFactory =
                typeof(TSubscriber).TryCreateInstance( new Dictionary<Type, object>{ { typeof( IEventStoreSubscriberContext ), context } } ) ??
                typeof(TSubscriber).TryCreateInstance( new Dictionary<Type, object>() );

            if( subscriberFactory  == null )
                throw new InvalidOperationException( $"Can't create {typeof( TSubscriber ).Name} instance, because there is no public constructore" );

            return subscriberFactory.OfType<TSubscriber>();
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

        private List<IEventSubscriber> GetSubscribersInstances()
        {
            return EventSubscribers.Values
                .Select( factory => factory() )
                .Distinct()
                .ToList();
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
            CurrentUserProvider = InitializeProperty<ICurrentUserProvider>( () => new CurrentUserProviderStub { CurrentUserId = Guid.Empty } );

            if ( initialize )
                Initialize();
        }
       
        #region IEventStoreKitServiceBuilder implementation
        
        public ServiceProperty<IEventStoreConfiguration> Configuration { get; }
        public ServiceProperty<ILoggerFactory> LoggerFactory { get; }
        public ServiceProperty<IScheduler> Scheduler { get; }
        public ServiceProperty<IDbProviderFactory> DbProviderFactorySubscriber { get; }
        public ServiceProperty<IDbProviderFactory> DbProviderFactoryEventStore { get; }
        public ServiceProperty<ICurrentUserProvider> CurrentUserProvider { get; }

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

        public IEventStoreKitServiceBuilder SetCurrentUserProvider( ICurrentUserProvider currentUserProvider )
        {
            CurrentUserProvider.Value = currentUserProvider;
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

        public IEventStoreKitServiceBuilder RegisterAggregateCommandHandler<TAggregate>() where TAggregate : ICommandHandler, IAggregate
        {
            var aggregateType = typeof(TAggregate);
            RegisterAggregateCommandHandler( aggregateType );
            
            return this;
        }

        public IEventStoreKitServiceBuilder RegisterAggregateCommandHandler( Type aggregateType )
        {
            if( !typeof( IAggregate ).IsAssignableFrom( aggregateType ) )
                throw new ArgumentException();

            AggregateCommandHandlers.Add( aggregateType );

            if( Initialized )
                ConfigureAggregateCommandHandlerRouts( aggregateType );

            return this;
        }

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

        public IEventStoreKitServiceBuilder RegisterSaga<TSaga>(
            Dictionary<Type, Func<Message, string>> sagaIdResolve = null,
            Func<IEventStoreKitService, string, TSaga> sagaFactory = null,
            bool cached = false )
            where TSaga : class, ISaga
        {
            var sagaType = typeof( TSaga );
            var sagaConstructor = sagaFactory.With( ctor => new Func<string,ISaga>( id => ctor( this, id ) ) );

            // todo: cache

            var sagaRegistration = new SagaRegistrationInfo
            {
                SagaType = sagaType,
                FactoryMethod = sagaConstructor,
                IdResolvingMap = sagaIdResolve
            };
            SagaRegistration.Add( sagaRegistration );

            if ( Initialized )
                ConfigureSagasRouts( sagaRegistration );

            return this;
        }

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
        
        public void SendCommand<TCommand>( TCommand command ) where TCommand : DomainCommand
        {
            CommandBus.SendCommand( command );
        }

        public void RaiseEvent( DomainEvent message )
        {
            if ( message.CreatedBy == Guid.Empty && CurrentUserProvider.Value.CurrentUserId != null )
                message.CreatedBy = CurrentUserProvider.Value.CurrentUserId.Value;
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
            // todo: send message through the publisher
            var targets = subscribers.Any() ? subscribers.ToList() : GetSubscribersInstances();
            var tasks = targets.Select( t => t.QueuedMessages() ).ToArray();
            Task.WaitAll( tasks );
        }

        public void CleanData()
        {
            StoreEvents.Advanced.Purge();

            var msg = new SystemCleanedUpEvent();
            var tasks = GetSubscribersInstances()
                .Select( s =>
                {
                    s.Handle( msg );
                    return s.QueuedMessages();
                } )
                .ToArray();
            Task.WaitAll( tasks );
        }

        #endregion

        public void Dispose()
        {
            StoreEvents?.Dispose();
        }
    }
}