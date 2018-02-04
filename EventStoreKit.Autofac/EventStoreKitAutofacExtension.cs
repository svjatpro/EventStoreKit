using System;
using System.Linq;
using Autofac;
using Autofac.Core;
using Autofac.Features.GeneratedFactories;
using EventStoreKit.Handler;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;

namespace EventStoreKit.Autofac
{
    public static class EventStoreKitAutofacExtension
    {
            public static void InitializeEventStoreKitService(
                this ContainerBuilder builder,
                Func<IComponentContext, EventStoreKitService> initializer = null )
            {
                // register default EventStoreConfiguration
                builder
                    .RegisterInstance(
                        new EventStoreConfiguration
                        {
                            InsertBufferSize = 10000,
                            OnIddleInterval = 500
                        } )
                    .As<IEventStoreConfiguration>()
                    .IfNotRegistered( typeof( IEventStoreConfiguration ) );

                // register service
                builder
                    .Register( ctx =>
                    {
                        var service = initializer.With( initialize => initialize( ctx ) ) ?? new EventStoreKitService();

                        ctx.ResolveOptional<IEventStoreConfiguration>().Do( config => service.SetConfiguration( config ) );

                    // ctx.ResolveKeyed<IDbProviderFactory>( new DataBaseConfiguration( connectionString ) );

                    // Register event handlers
                    var cmdHandlers = ctx
                            .ComponentRegistry
                            .Registrations
                            .Where( registration => registration.Activator.LimitType.IsAssignableTo<ICommandHandler>() )
                            .Select( registration =>
                            {
                                var factoryGenerator = new FactoryGenerator( typeof( Func<ICommandHandler> ), registration, ParameterMapping.ByType );
                                return (Func<ICommandHandler>)factoryGenerator.GenerateFactory( ctx, new Parameter[] { } );
                            } )
                            .Where( h => h != null )
                            .ToList();
                        cmdHandlers.ForEach( handler => service.RegisterCommandHandler( handler ) );

                    // Register event subscribers
                    var subscribers = ctx
                            .ComponentRegistry
                            .Registrations
                            .Where( r => r.Activator.LimitType.IsAssignableTo<IEventSubscriber>() )
                            .Select( r =>
                                ctx.IsRegistered( r.Activator.LimitType ) ?
                                ctx.Resolve( r.Activator.LimitType ) :
                                r.Services.FirstOrDefault().With( ctx.ResolveService ) )
                            .Select( h => h.OfType<IEventSubscriber>() )
                            .Where( h => h != null )
                            .ToList();
                    //subscribers.ForEach( s => service.RegisterEventSubscriber(  ) );

                    return service;
                    } )
                    .As<IEventStoreKitService>()
                    .AutoActivate()
                    .SingleInstance();
            }
    }

//    public class EventStoreModule : Module
//    {
//        #region Private fields

//        private readonly Type SqlDialectType;
//        private readonly string ConfigurationString;
//        private readonly string ConnectionString;
//        private readonly Dictionary<Type,string> ProvidersMap = new Dictionary<Type, string>
//        {
//            { typeof( MsSqlDialect ), "System.Data.SqlClient" },
//            { typeof( MySqlDialect ), "MySql.Data.MySqlClient" }
//        };

//        #endregion

//        private class Startup : IStartable
//        {
//            private readonly IIdGenerator IdGenerator;
//            private readonly ILogger<EventStoreModule> Logger;
//            private readonly ICurrentUserProvider CurrentUserProvider;
//            private readonly IComponentContext Container;
//            private readonly IEventDispatcher Dispatcher;
//            private readonly IEnumerable<ICommandHandler> CommandHandlers;
//            private readonly IEnumerable<IEventSubscriber> Subscribers;

//// ReSharper disable UnusedMember.Local
//            private void RegisterCommandHandler<TCommand, TEntity>()
//// ReSharper restore UnusedMember.Local
//                where TCommand : DomainCommand
//                where TEntity : class, ITrackableAggregate
//            {
//                // register Action as handler to dispatcher
//                var repositoryFactory = Container.Resolve<Owned<Func<IRepository>>>();
//                var handlerFactory = Container.Resolve<Owned<Func<ICommandHandler<TCommand, TEntity>>>>();

//                var handleAction = new Action<TCommand>( cmd =>
//                {
//                    var repository = repositoryFactory.Value();
//                    var handler = handlerFactory.Value();

//                    if( cmd.Created == default( DateTime ) )
//                        cmd.Created = DateTime.Now;
//                    if ( cmd.CreatedBy == Guid.Empty && CurrentUserProvider.CurrentUserId != null )
//                        cmd.CreatedBy = CurrentUserProvider.CurrentUserId.Value;
//                    var context = new CommandHandlerContext<TEntity>
//                    {
//                        Entity = repository.GetById<TEntity>( cmd.Id )
//                    };
//                    if ( cmd.CreatedBy != Guid.Empty )
//                        context.Entity.IssuedBy = cmd.CreatedBy;
//                    else
//                        CurrentUserProvider.CurrentUserId.Do( userId => context.Entity.IssuedBy = userId.GetValueOrDefault() );

//                    handler.Handle( cmd, context );
//                    Logger.Info( "{0} processed; version = {1}", cmd.GetType().Name, cmd.Version );
//                    repository.Save( context.Entity, IdGenerator.NewGuid() );
//                } );
//                Dispatcher.RegisterHandler( handleAction );
//            }

//            public Startup(
//                ICurrentUserProvider currentUserProvider,
//                IComponentContext container, 
//                IEventDispatcher dispatcher,
//                IEnumerable<ICommandHandler> commandHandlers,
//                ILogger<EventStoreModule> logger,
//                IIdGenerator idGenerator, 
//                IEnumerable<IEventSubscriber> subscribers)
//            {
//                CurrentUserProvider = currentUserProvider;
//                Container = container;
//                Dispatcher = dispatcher.CheckNull( "dispatcher" );
//                CommandHandlers = commandHandlers;
//                Logger = logger;
//                Subscribers = subscribers;
//                IdGenerator = idGenerator;
//            }

//            #region Implementation of IStartable

//            public void Start()
//            {
//                #region Events

//                var dispatcherType = Dispatcher.GetType();
//                var subscriberType = typeof( IEventSubscriber );
//                foreach ( var subscriber in Subscribers )
//                {
//                    foreach ( var handledEventType in subscriber.HandledEventTypes )
//                    {
//                        var registerMethod = dispatcherType.GetMethod( "RegisterHandler" ).MakeGenericMethod( handledEventType );
//                        var handleMethod = subscriberType.GetMethods().Single( m => m.Name == "Handle" );
//                        var handleDelegate = Delegate.CreateDelegate( typeof( Action<Message> ), subscriber, handleMethod );
//                        registerMethod.Invoke( Dispatcher, new object[] { handleDelegate } );
//                    }
//                }

//                #endregion

//                #region Commands

//                var commandHandlerInterfaceType = typeof( ICommandHandler<,> );
//                var registerCommandMehod = GetType().GetMethod( "RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance );
//                CommandHandlers
//                    .ToList()
//                    .ForEach( handler =>
//                    {
//                        var cmdHandlerTypes = handler
//                            .GetType()
//                            .GetInterfaces()
//                            .Where( i => i.Name == commandHandlerInterfaceType.Name );
//                        cmdHandlerTypes
//                            .ToList()
//                            .ForEach( cmdType =>
//                            {
//                                var genericArgs = cmdType.GetGenericArguments();
//                                registerCommandMehod
//                                    .MakeGenericMethod( genericArgs[0], genericArgs[1] )
//                                    .Invoke( this, new object[] { } );
//                            } );
//                    } );

//                #endregion
//            }

//            #endregion
//        }

//        protected override void Load( ContainerBuilder builder )
//        {
//            base.Load( builder );
//            builder.RegisterType<Startup>().As<IStartable>();

//            builder.RegisterGeneric(typeof(LoggerStub<>)).As(typeof(ILogger<>));
//            builder.RegisterType<ConflictDetector>().As<IDetectConflicts>();
//            builder.RegisterType<EventStoreRepository>().As<IRepository>().ExternallyOwned();
//            builder.RegisterType<SagaEventStoreRepository>().As<ISagaRepository>().ExternallyOwned();

//            builder.RegisterType<SequentialIdgenerator>().As<IIdGenerator>();
//            builder.RegisterType<EntityFactory>().As<IConstructAggregates>();
//            builder.RegisterType<SagaFactory>()
//                .As<IConstructSagas>()
//                .As<ISagaFactory>()
//                .SingleInstance();
            
//            builder.RegisterType<EventSequence>().SingleInstance();
//            builder.Register( ctx => ( new NewThreadScheduler( action => new Thread( action ) { IsBackground = true } ) ) ).As<IScheduler>();

//            builder.RegisterType<MessageDispatcher>()
//                .As<IEventPublisher>()
//                .As<IEventDispatcher>()
//                .As<ICommandBus>()
//                .SingleInstance();

//            builder
//                .Register( ctx =>
//                    new EventStoreAdapter(
//                        CreateWireup( ctx ),
//                        ctx.Resolve<ILogger<EventStoreAdapter>>(),
//                        ctx.Resolve<IEventPublisher>(),
//                        ctx.Resolve<ICommandBus>() ) )
//                .As<IStoreEvents>()
//                .SingleInstance();

//            builder.RegisterType<CommitsIteratorByPeriod>().As<ICommitsIterator>().ExternallyOwned();
//            builder
//                .RegisterType<ReplayHistoryService>()
//                .As<IReplaysHistory>()
//                .SingleInstance();

//            builder
//                .Register( ctx => 
//                    new EventStoreConfiguration
//                    {
//                        InsertBufferSize = 10000,
//                        OnIddleInterval = 500
//                    } )
//                .As<IEventStoreConfiguration>()
//                .SingleInstance();

//            builder
//                .Register( ctx => new CurrentUserProviderStub { CurrentUserId = Guid.NewGuid() } )
//                .As<ICurrentUserProvider>()
//                .SingleInstance();

//            builder
//                .Register( context => new DbProviderFactoryStub() )
//                .As<IDbProviderFactory>()
//                .SingleInstance();
//            builder
//                .Register( c => c.Resolve<IDbProviderFactory>().Create() )
//                .As<IDbProvider>()
//                .SingleInstance();
//        }

//        private Wireup CreateWireup( IComponentContext ctx )
//        {
//            var wireup = Wireup.Init();

//            var logFactory = ctx.Resolve<Func<ILogger<EventStoreAdapter>>>();
//            wireup.LogTo( type => logFactory() );

//            var persistanceWireup = 
//                ConfigurationString != null ? 
//                wireup.UsingSqlPersistence( ConfigurationString ) : 
//                wireup.UsingSqlPersistence( null, ProvidersMap[SqlDialectType], ConnectionString );

//            return persistanceWireup
//                .WithDialect( (ISqlDialect)Activator.CreateInstance( SqlDialectType ) )
//                .PageEvery( 1024 )
//                .InitializeStorageEngine()
//                .UsingJsonSerialization();
//        }

//        public EventStoreModule( Type sqlDialectType, string configurationString = null, string connectionString = null )
//        {
//            SqlDialectType = sqlDialectType.CheckNull( "sqlDialectType" );
//            if( configurationString == null && connectionString == null )
//                throw new ArgumentNullException( "connectionString" );

//            ConfigurationString = configurationString;
//            ConnectionString = connectionString;
//        }
//    }
}