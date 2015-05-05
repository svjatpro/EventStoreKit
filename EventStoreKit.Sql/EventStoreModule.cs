using System;
using System.Collections.Generic;
using System.Linq;
using System.Monads;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Threading;
using Autofac;
using Autofac.Features.OwnedInstances;
using CommonDomain;
using CommonDomain.Core;
using CommonDomain.Persistence;
using CommonDomain.Persistence.EventStore;
using EventStoreKit.Aggregates;
using EventStoreKit.CommandBus;
using EventStoreKit.Constants;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.IdGenerators;
using NEventStore;
using NEventStore.Persistence.Sql.SqlDialects;
using Module = Autofac.Module;

namespace EventStoreKit.Sql
{
    public class EventStoreModule : Module
    {
        private class Startup : IStartable
        {
            private readonly ILogger<EventStoreModule> Logger;
            private readonly ISecurityManager SecurityManager;
            private readonly IComponentContext Container;
            private readonly IEventDispatcher Dispatcher;
            private readonly IEnumerable<ICommandHandler> CommandHandlers;
            private readonly IEnumerable<IEventSubscriber> Subscribers;

            private void RegisterCommandHandler<TCommand, TEntity>()
                where TCommand : DomainCommand
                where TEntity : class, ITrackableAggregate
            {
                // register Action as handler to dispatcher
                var repositoryFactory = Container.Resolve<Owned<Func<IRepository>>>();
                var handlerFactory = Container.Resolve<Owned<Func<ICommandHandler<TCommand, TEntity>>>>();

                var handleAction = new Action<TCommand>( cmd =>
                {
                    var repository = repositoryFactory.Value();
                    var handler = handlerFactory.Value();

                    if( cmd.Created == default( DateTime ) )
                        cmd.Created = DateTime.Now;
                    SecurityManager.CurrentUser.Do( user => cmd.CreatedBy = user.UserId );
                    var context = new CommandHandlerContext<TEntity>
                    {
                        Entity = repository.GetById<TEntity>( cmd.Id )
                    };
                    SecurityManager.CurrentUser.Do( user => context.Entity.IssuedBy = user.UserId );

                    handler.Handle( cmd, context );
                    Logger.InfoFormat( "{0} processed; version = {1}", cmd.GetType().Name, cmd.Version );
                    repository.Save( context.Entity, Guid.NewGuid() ); // todo: idgenerator
                } );
                Dispatcher.RegisterHandler( handleAction );
            }

            public Startup(
                ISecurityManager securityManager,
                IComponentContext container, 
                IEventDispatcher dispatcher, 
                IEnumerable<ICommandHandler> commandHandlers,
                ILogger<EventStoreModule> logger,
                IEnumerable<IEventSubscriber> subscribers )
            {
                SecurityManager = securityManager;
                Container = container;
                Dispatcher = dispatcher.CheckNull( "dispatcher" );
                CommandHandlers = commandHandlers;
                Logger = logger;
                Subscribers = subscribers;
            }

            #region Implementation of IStartable

            public void Start()
            {
                #region Events

                var dispatcherType = Dispatcher.GetType();
                var subscriberType = typeof( IEventSubscriber );
                foreach ( var subscriber in Subscribers )
                {
                    foreach ( var handledEventType in subscriber.HandledEventTypes )
                    {
                        var registerMethod = dispatcherType.GetMethod( "RegisterHandler" ).MakeGenericMethod( handledEventType );
                        var handleMethod = subscriberType.GetMethods().Single( m => m.Name == "Handle" );
                        var handleDelegate = Delegate.CreateDelegate( typeof( Action<Message> ), subscriber, handleMethod );
                        registerMethod.Invoke( Dispatcher, new object[] { handleDelegate } );
                    }
                }

                #endregion

                #region Commands

                var commandHandlerInterfaceType = typeof( ICommandHandler<,> );
                var registerCommandMehod = GetType().GetMethod( "RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance );
                CommandHandlers
                    .ToList()
                    .ForEach( handler =>
                    {
                        var cmdHandlerTypes = handler
                            .GetType()
                            .GetInterfaces()
                            .Where( i => i.Name == commandHandlerInterfaceType.Name );
                        cmdHandlerTypes
                            .ToList()
                            .ForEach( cmdType =>
                            {
                                var genericArgs = cmdType.GetGenericArguments();
                                registerCommandMehod
                                    .MakeGenericMethod( genericArgs[0], genericArgs[1] )
                                    .Invoke( this, new object[] { } );
                            } );
                    } );

                #endregion
            }

            #endregion
        }

        protected override void Load( ContainerBuilder builder )
        {
            base.Load( builder );
            builder.RegisterType<Startup>().As<IStartable>();

            builder.RegisterType<ConflictDetector>().As<IDetectConflicts>();
            builder.RegisterType<EventStoreRepository>().As<IRepository>().ExternallyOwned();
            builder.RegisterType<SagaEventStoreRepository>().As<ISagaRepository>().ExternallyOwned();

            builder.RegisterGeneric( typeof( Logger<> ) ).As( typeof( ILogger<> ) );

            builder.RegisterType<SequentialIdgenerator>().As<IIdGenerator>();
            builder.RegisterType<EntityFactory>().As<IConstructAggregates>();
            
            builder.RegisterType<EventSequence>().SingleInstance();

            builder.Register( ctx => ( new NewThreadScheduler( action => new Thread( action ) { IsBackground = true } ) ) ).As<IScheduler>();

            builder.RegisterType<MessageDispatcher>()
                .As<IEventPublisher>()
                .As<IEventDispatcher>()
                .As<ICommandBus>()
                .SingleInstance();

            builder
                .Register( ctx =>
                    new EventStoreAdapter(
                        CreateWireup( ctx ),
                        ctx.Resolve<ILogger<EventStoreAdapter>>(),
                        ctx.Resolve<IEventPublisher>(),
                        ctx.Resolve<ICommandBus>() ) )
                .As<IStoreEvents>()
                //.As<IReplaysHistory>()
                .SingleInstance();

            builder.RegisterType<ReplayHistoryService>().As<IReplaysHistory>().SingleInstance();
        }

        private Wireup CreateWireup( IComponentContext ctx )
        {
            var wireup = Wireup.Init();
            wireup.LogTo( type => new Log4NetLogger( type ) );
            var persistanceWireup = wireup
                .UsingSqlPersistence( ctx.ResolveNamed<string>( EventStoreConstants.CommitsConfigNameTag ) )
                .WithDialect( new MsSqlDialect() )
                .PageEvery( 1024 );

            return persistanceWireup
                //.EnlistInAmbientTransaction()
                .InitializeStorageEngine()
                .UsingJsonSerialization();
        }
    }
}