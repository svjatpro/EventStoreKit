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

        private IDbProviderFactory DbProviderFactorySubscribers = null;
        private IDbProviderFactory DbProviderFactoryEventStore = null;

        private readonly Dictionary<Type, Func<IEventSubscriber>> EventSubscribers = new Dictionary<Type, Func<IEventSubscriber>>();

        #endregion

        #region Private methods

        private void InitializeCommon()
        {
            DbProviderFactorySubscribers = new DbProviderFactoryStub();
            DbProviderFactoryEventStore = DbProviderFactorySubscribers;

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
        private IDbProviderFactory InitializeDbProviderFactory( Type factoryType, IDataBaseConfiguration config )
        {
            var factory =
                TryInitializeDbProvider( factoryType, new Dictionary<Type, object> { { typeof( IDataBaseConfiguration ), config } } ) ??
                TryInitializeDbProvider( factoryType, new Dictionary<Type, object> { { typeof( string ), config.ConfigurationString } } ) ??
                TryInitializeDbProvider( factoryType, new Dictionary<Type, object> { { typeof( DataBaseConnectionType ), config.DataBaseConnectionType }, { typeof( string ), config.ConnectionString } } );
            if( factory == null )
                throw new InvalidOperationException( $"Can't create {factoryType.Name} instance, because there is no appropriate constructor" );

            return factory;
        }

        private IEventStoreSubscriberContext CreateEventSubscriberContext<TSubscriber>( IDataBaseConfiguration config = null ) where TSubscriber : class, IEventSubscriber
        {
            var dbFactory = config.Return(
                c => InitializeDbProviderFactory( DbProviderFactorySubscribers.GetType(), config ),
                DbProviderFactorySubscribers );
            return new EventStoreSubscriberContext
            {
                Logger = ResolveLogger<TSubscriber>(),
                Scheduler = Scheduler,
                Configuration = Configuration,
                DbProviderFactory = dbFactory
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
        private void RegisterEventSubscriber( Func<IEventSubscriber> subscriberFactory, IEventStoreSubscriberContext context )
        {
            var dispatcherType = Dispatcher.GetType();
            var subscriberInstance = subscriberFactory();
            var subscriberType = subscriberInstance.GetType();
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

            EventSubscribers.Add( subscriberType, subscriberFactory );
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

        public EventStoreKitService()
        {
            InitializeCommon();
            InitializeEventStore();
        }

        #region Event Subscribers methods

        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory )
            where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>();
            RegisterEventSubscriber( () => subscriberFactory( context ), context );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory, IDataBaseConfiguration configuration )
            where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( configuration );
            RegisterEventSubscriber( () => subscriberFactory( context ), context );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( IDataBaseConfiguration configuration ) where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>( configuration );
            var subscriber = InitializeEventSubscriber<TSubscriber>( context );
            RegisterEventSubscriber( () => subscriber, context );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>()
            where TSubscriber : class, IEventSubscriber
        {
            var context = CreateEventSubscriberContext<TSubscriber>();
            var subscriber = InitializeEventSubscriber<TSubscriber>( context );

            RegisterEventSubscriber( () => subscriber, context );
            return this;
        }
        public EventStoreKitService RegisterEventSubscriber( Func<IEventSubscriber> subscriberFactory )
        {
            RegisterEventSubscriber( subscriberFactory, null );
            return this;
        }

        #endregion

        #region DataBase configuring methods

        public EventStoreKitService SetDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
            where TDbProviderFactory : IDbProviderFactory
        {
            SetSubscriberDataBase<TDbProviderFactory>( configuration );
            SetEventStoreDataBase<TDbProviderFactory>( configuration );
            return this;
        }
        public EventStoreKitService SetSubscriberDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
        {
            DbProviderFactorySubscribers = InitializeDbProviderFactory( typeof( TDbProviderFactory ), configuration );
            return this;
        }
        public EventStoreKitService SetEventStoreDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration )
        {
            DbProviderFactoryEventStore = InitializeDbProviderFactory( typeof( TDbProviderFactory ), configuration );
            InitializeEventStore();
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

        public EventStoreKitService SetConfiguration( IEventStoreConfiguration configuration )
        {
            Configuration = configuration;
            return this;
        }
        
        #region IEventStoreKitService implementation

        public IEventStoreConfiguration GetConfiguration()
        {
            return Configuration;
        }

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