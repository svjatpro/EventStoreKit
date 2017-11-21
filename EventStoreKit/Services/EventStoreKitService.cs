using System;
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

namespace EventStoreKit.Services
{
    public interface IEventStoreKitService
    {
        void SendCommand( DomainCommand command );
    }
    public class EventStoreKitService : IEventStoreKitService
    {
        private IEventDispatcher Dispatcher;
        private IEventPublisher EventPublisher;
        private ICommandBus CommandBus;
        private IStoreEvents StoreEvents;
        private IConstructAggregates ConstructAggregates;
        private ICurrentUserProvider CurrentUserProvider;
        private IIdGenerator IdGenerator;
        private IScheduler Scheduler;
        private IEventStoreConfiguration Configuration;
        private IDbProviderFactory DbProviderFactory;

        private static Dictionary<Type, ICommandHandler> CommandHandlerTypes = new Dictionary<Type, ICommandHandler>();

        protected virtual ICurrentUserProvider ResolveCurrentUserProvider() { return CurrentUserProvider; }
        protected virtual ILogger<T> ResolveLogger<T>() { return new LoggerStub<T>(); }
        protected virtual IRepository ResolveRepository()
        {
            return new EventStoreRepository( StoreEvents, ConstructAggregates, new ConflictDetector() );
        }

        private void InitializeEventStore()
        {
            var dispatcher = new MessageDispatcher(ResolveLogger<MessageDispatcher>());
            Dispatcher = dispatcher;
            EventPublisher = dispatcher;
            CommandBus = dispatcher;
            StoreEvents = new EventStoreAdapter(CreateWireup(), ResolveLogger<EventStoreAdapter>(), EventPublisher,CommandBus);
            ConstructAggregates = new EntityFactory();

            CurrentUserProvider = new CurrentUserProviderStub {CurrentUserId = Guid.NewGuid()};
            IdGenerator = new SequentialIdgenerator();
            Scheduler = new NewThreadScheduler(action => new Thread(action) { IsBackground = true }); // todo:
            Configuration = new EventStoreConfiguration // todo:
            {
                InsertBufferSize = 10000,
                OnIddleInterval = 500
            };
            DbProviderFactory = new DbProviderFactoryStub(); // todo:
        }
        private Wireup CreateWireup()
        {
            var wireup = Wireup.Init();
            wireup.LogTo(type => ResolveLogger<EventStoreAdapter>());
            return wireup.UsingInMemoryPersistence();
        }

        private void RegisterCommandHandler<TCommand, TEntity>()
            // ReSharper restore UnusedMember.Local
            where TCommand : DomainCommand
            where TEntity : class, ITrackableAggregate
        {
            // register Action as handler to dispatcher
            var logger = ResolveLogger<EventStoreKitService>();
            var repositoryFactory = new Func<IRepository>( ResolveRepository );
            var handlerFactory = new Func<ICommandHandler<TCommand,TEntity>>(() => (ICommandHandler<TCommand, TEntity>) CommandHandlerTypes[typeof(ICommandHandler<TCommand, TEntity>)]);

            var handleAction = new Action<TCommand>( cmd =>
            {
                var repository = repositoryFactory();
                var handler = handlerFactory();

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

        public static void RegisterCommandHandler<THandler>( THandler handler = null ) where THandler : class, ICommandHandler, new()
        {
            // todo: remove constraints for class and new(), try to create by reflectin, otherwise throw exception
            // but user is able to register subscriber instance, which has no parameterless constructor
            if ( handler == null )
                handler = new THandler();
            var commandHandlerInterfaceType = typeof(ICommandHandler<,>);
            typeof(THandler)
                .GetInterfaces()
                .Where(i => i.Name == commandHandlerInterfaceType.Name)
                .ToList()
                .ForEach(h => CommandHandlerTypes.Add(h, handler ));
        }

        public EventStoreKitService RegisterEventsSubscriber<TSubscriber>( IEventSubscriber subscriber = null ) where TSubscriber : class, IEventSubscriber
        {
            if (subscriber == null) // todo: use GetConstructors
            {
                var stype = typeof(TSubscriber);
                var ctor = stype
                    .GetConstructor(new[]
                    {
                        typeof (ILogger),
                        typeof (IScheduler),
                        typeof (IEventStoreConfiguration),
                        typeof (IDbProviderFactory)
                    });

                //if (ctor == null)
                //    throw new InvalidOperationException(stype.Name + " doesn't have constructor ( Action<Type,Action<Message>,bool>, Func<IDbProvider>, IEventStoreConfiguration, ILogger, ProjectionTemplateOptions )");
                subscriber = (TSubscriber)(ctor.Invoke(new object[] { ResolveLogger<TSubscriber>(), Scheduler, Configuration, DbProviderFactory }));
            }
            //ILogger logger, IScheduler scheduler, IEventStoreConfiguration config, IDbProviderFactory dbProviderFactory

            var dispatcherType = Dispatcher.GetType();
            var subscriberType = typeof(IEventSubscriber);
            foreach (var handledEventType in subscriber.HandledEventTypes)
            {
                var registerMethod = dispatcherType.GetMethod("RegisterHandler").MakeGenericMethod(handledEventType);
                var handleMethod = subscriberType.GetMethods().Single(m => m.Name == "Handle");
                var handleDelegate = Delegate.CreateDelegate(typeof(Action<Message>), subscriber, handleMethod);
                registerMethod.Invoke(Dispatcher, new object[] { handleDelegate });
            }

            return this;
        }

        private EventStoreKitService()
        {
            
        }

        public static EventStoreKitService Initialize()
        {
            var service = new EventStoreKitService();
            service.InitializeEventStore();

            // handlers for Aggregates
            //var commandHandlerInterfaceType = typeof(ICommandHandler<,>);
            var registerCommandMehod = service.GetType().GetMethod("RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance);

            CommandHandlerTypes
                .ToList()
                .ForEach(cmdType =>
                {
                    var genericArgs = cmdType.Key.GetGenericArguments();
                    registerCommandMehod
                        .MakeGenericMethod(genericArgs[0], genericArgs[1])
                        .Invoke(service, new object[] { });
                });

            return service;
        }

        public void SendCommand(DomainCommand command)
        {
            CommandBus.Send( command );
        }
    }
}
