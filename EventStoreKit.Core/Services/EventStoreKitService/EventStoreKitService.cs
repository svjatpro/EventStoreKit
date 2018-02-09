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
    public interface IEventStoreKitServiceBuilder
    {
        //
        ServiceProperty<IEventStoreConfiguration> Configuration { get; }
        ServiceProperty<ILoggerFactory> LoggerFactory { get; }

        IEventStoreKitService Initialize();
    }

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
        private IScheduler Scheduler;
        
        private IDbProviderFactory DbProviderFactorySubscribers = null;
        private IDbProviderFactory DbProviderFactoryEventStore = null;

        private readonly Dictionary<Type, Func<IEventSubscriber>> EventSubscribers = new Dictionary<Type, Func<IEventSubscriber>>();
        private readonly List<IServiceProperty> ServiceProperties = new List<IServiceProperty>();

        private bool Initialized;

        #endregion

        #region Private methods

        private void InitializeCommon()
        {
            if( DbProviderFactorySubscribers == null )
                DbProviderFactorySubscribers = new DbProviderFactoryStub();
            if( DbProviderFactoryEventStore == null )
                DbProviderFactoryEventStore = DbProviderFactorySubscribers;

            IdGenerator = new SequentialIdgenerator();

            if( Scheduler == null )
            {
                Scheduler = new NewThreadScheduler( action => new Thread(action) { IsBackground = true } ); // todo:
            }

            ServiceProperties.ForEach( property => property.Initialize() );

            CurrentUserProvider = new CurrentUserProviderStub { CurrentUserId = Guid.NewGuid() };

            var dispatcher = new MessageDispatcher( LoggerFactory.Value.Create<MessageDispatcher>() );
            Dispatcher = dispatcher;
            EventPublisher = dispatcher;
            CommandBus = dispatcher;

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

            if ( DbProviderFactoryEventStore == null || DbProviderFactoryEventStore.DefaultDataBaseConfiguration.DataBaseConnectionType == DataBaseConnectionType.None )
            {
                return wireup.UsingInMemoryPersistence();
            }
            else
            {
                var configuration = DbProviderFactoryEventStore.DefaultDataBaseConfiguration;
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

        private void RegisterCommandHandler<TCommand, TEntity>( Func<ICommandHandler<TCommand, TEntity>> handlerFactory )
            where TCommand : DomainCommand
            where TEntity : class, ITrackableAggregate
        {
            // register Action as handler to dispatcher
            var repositoryFactory = new Func<IRepository>( ResolveRepository );

            var handleAction = new Action<TCommand>( cmd =>
            {
                var repository = repositoryFactory();
                var handler = handlerFactory();
                var logger = LoggerFactory.Value.Create<EventStoreKitService>();

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
                TryCreateInstance( factoryType, new Dictionary<Type, object> { { typeof( DataBaseConnectionType ), config.DataBaseConnectionType }, { typeof( string ), config.ConnectionString } } );
            if( factory == null )
                throw new InvalidOperationException( $"Can't create {factoryType.Name} instance, because there is no appropriate constructor" );

            return factory.OfType<IDbProviderFactory>();
        }

        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber>( IDataBaseConfiguration config = null ) where TSubscriber : class, IEventSubscriber
        {
            var dbFactory = config.Return(
                c => InitializeDbProviderFactory( DbProviderFactorySubscribers.GetType(), config ),
                DbProviderFactorySubscribers );
            return new EventStoreSubscriberContext
            {
                Logger = LoggerFactory.GetValueOrDefault().Create<TSubscriber>(),
                Scheduler = Scheduler,
                Configuration = Configuration.GetValueOrDefault(),
                DbProviderFactory = dbFactory
            };
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
            EventSubscribers.Add( subscriberType, subscriberFactory );
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

        #region Protected methods

        protected virtual ICurrentUserProvider ResolveCurrentUserProvider() { return CurrentUserProvider; }
        protected virtual IRepository ResolveRepository()
        {
            return new EventStoreRepository(StoreEvents, ConstructAggregates, new ConflictDetector());
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

            if ( initialize )
                Initialize();
        }

        public IEventStoreKitService Initialize()
        {
            if ( !Initialized )
            {
                InitializeCommon();
            }
            InitializeEventStore();
            Initialized = true;

            return this;
        }

        public EventStoreKitService ReInitialize()
        {
            if( Initialized )
                Initialize();
            return this;
        }

        #region Event Subscribers methods

        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory )
            where TSubscriber : class, IEventSubscriber
        {
            RegisterEventSubscriberFactory( () => subscriberFactory( CreateEventSubscriberContext<TSubscriber>() ) );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory, IDataBaseConfiguration configuration )
            where TSubscriber : class, IEventSubscriber
        {
            RegisterEventSubscriberFactory( () => subscriberFactory( CreateEventSubscriberContext<TSubscriber>( configuration ) ) );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( IDataBaseConfiguration configuration ) where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( configuration );
            var subscriber = InitializeEventSubscriber<TSubscriber>( context );
            RegisterEventSubscriberFactory( () => subscriber );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>()
            where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>();
            var subscriber = InitializeEventSubscriber<TSubscriber>( context );
            RegisterEventSubscriberFactory( () => subscriber );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber( Func<IEventSubscriber> subscriberFactory )
        {
            RegisterEventSubscriberFactory( subscriberFactory );
            return this;
        }

        public Dictionary<Type, Func<IEventSubscriber>> GetEventSubscribers()
        {
            return EventSubscribers;
        }

        #endregion

        #region DataBase configuring methods

        public EventStoreKitService SetDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
            where TDbProviderFactory : IDbProviderFactory
        {
            return SetDataBase( typeof(TDbProviderFactory), configuration );
        }
        public EventStoreKitService SetDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration )
        {
            SetSubscriberDataBase( dbProviderFactoryType, configuration );
            SetEventStoreDataBase( dbProviderFactoryType, configuration );
            return this;
        }
        public EventStoreKitService SetDataBase( IDbProviderFactory factory )
        {
            SetSubscriberDataBase( factory );
            SetEventStoreDataBase( factory );
            return this;
        }


        public EventStoreKitService SetSubscriberDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
        {
            return SetSubscriberDataBase( typeof( TDbProviderFactory ), configuration );
        }
        public EventStoreKitService SetSubscriberDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration )
        {
            return SetSubscriberDataBase( InitializeDbProviderFactory( dbProviderFactoryType, configuration ) );
        }
        public EventStoreKitService SetSubscriberDataBase( IDbProviderFactory factory )
        {
            DbProviderFactorySubscribers = factory;
            return this;
        }

        public EventStoreKitService SetEventStoreDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
        {
            return SetEventStoreDataBase( typeof( TDbProviderFactory ), configuration );
        }
        public EventStoreKitService SetEventStoreDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration )
        {
            return SetEventStoreDataBase( InitializeDbProviderFactory( dbProviderFactoryType, configuration ) );
        }
        public EventStoreKitService SetEventStoreDataBase( IDbProviderFactory factory )
        {
            DbProviderFactoryEventStore = factory;
            ReInitialize();
            return this;
        }

        public IDbProviderFactory GetDataBaseProviderFactory()
        {
            return DbProviderFactorySubscribers;
        }

        #endregion

        #region Register command handlers methods

        public EventStoreKitService RegisterCommandHandler<THandler>() where THandler : class, ICommandHandler, new()
        {
            return RegisterCommandHandler( () => new THandler() );
        }

        public EventStoreKitService RegisterCommandHandler<THandler>( Func<THandler> handlerFactory ) where THandler : ICommandHandler
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

            return this;
        }

        #endregion

        public EventStoreKitService SetScheduler( IScheduler scheduler )
        {
            Scheduler = scheduler;
            ReInitialize();
            return this;
        }

        #region IEventStoreKitServiceBuilder implementation

        public ServiceProperty<IEventStoreConfiguration> Configuration { get; }
        public ServiceProperty<ILoggerFactory> LoggerFactory { get; }

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
            var targets =
                subscribers.Any() ?
                subscribers.ToList() :
                EventSubscribers.Values.Select( factory => factory() ).ToList();

            var tasks = targets.Select( s => s.WaitMessagesAsync() ).ToArray();
            Task.WaitAll( tasks );
        }

        public void CleanData()
        {
            StoreEvents.Advanced.Purge();

            var msg = new SystemCleanedUpEvent();
            var tasks = EventSubscribers
                .Values.ToList()
                .Select( subscriberFactory =>
                {
                    var subscriber = subscriberFactory();
                    subscriber.Handle( msg );
                    return subscriber.WaitMessagesAsync();
                } )
                .ToArray();
            Task.WaitAll( tasks );
        }

        #endregion
    }
}