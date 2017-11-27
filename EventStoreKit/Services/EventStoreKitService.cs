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
    public interface IEventStoreSubscriberContext
    {
        ILogger Logger { get; }
        IScheduler Scheduler { get; }
        IEventStoreConfiguration Configuration { get; }
        IDbProviderFactory DbProviderFactory { get; }
    }

    public class EventStoreSubscriberContext : IEventStoreSubscriberContext
    {
        public ILogger Logger { get; set; }
        public IScheduler Scheduler { get; set; }
        public IEventStoreConfiguration Configuration { get; set; }
        public IDbProviderFactory DbProviderFactory { get; set; }
    }

    public interface IEventStoreKitService
    {
        TSubscriber ResolveSubscriber<TSubscriber>() where TSubscriber : IEventSubscriber;

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
        public IDbProviderFactory DbProviderFactory;
                
        private readonly Dictionary<Type, IEventSubscriber> EventSubscribers = new Dictionary<Type, IEventSubscriber>();

        #endregion
        
        #region Private methods

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

        //private void RegisterCommandHandler<TCommand, TEntity>( Func<ICommandHandler<TCommand, TEntity>> handlerFactory )
        private void RegisterCommandHandler<TCommand, TEntity>( ICommandHandler<TCommand, TEntity> handlerFactory )
        // ReSharper restore UnusedMember.Local
            where TCommand : DomainCommand
            where TEntity : class, ITrackableAggregate
        {
            // register Action as handler to dispatcher
            var logger = ResolveLogger<EventStoreKitService>();
            var repositoryFactory = new Func<IRepository>( ResolveRepository );
            //var handlerFactory = new Func<ICommandHandler<TCommand, TEntity>>(() => (ICommandHandler<TCommand, TEntity>)CommandHandlerTypes[typeof(ICommandHandler<TCommand, TEntity>)]);

            var handleAction = new Action<TCommand>( cmd =>
            {
                var repository = repositoryFactory();
                //var handler = handlerFactory();
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

        private IEventStoreSubscriberContext CreateContext<TSubscriber>() where TSubscriber : class, IEventSubscriber
        {
            return new EventStoreSubscriberContext
            {
                Logger = ResolveLogger<TSubscriber>(),
                Scheduler = Scheduler,
                Configuration = Configuration,
                DbProviderFactory = DbProviderFactory
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

        public EventStoreKitService()
        {
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
            var registerCommandMehod = GetType().GetMethod("RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance);

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


        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory)
            where TSubscriber : class, IEventSubscriber
        {
            return RegisterEventSubscriber<TSubscriber>( subscriberFactory( CreateContext<TSubscriber>() ) );
        }
        public EventStoreKitService RegisterEventSubscriber<TSubscriber>( IEventSubscriber subscriber = null ) 
            where TSubscriber : class, IEventSubscriber
        {
            if (subscriber == null)
            {
                var stype = typeof(TSubscriber);

                var ctor = stype
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault( c =>
                    {
                        var args = c.GetParameters();
                        return 
                            args.Length == 1 &&
                            args[0].ParameterType == typeof(IEventStoreSubscriberContext);
                    });
                if (ctor == null)
                    throw new  InvalidOperationException( $"Can't create {stype.Name} instance, because there is no public constructore" );
                
                subscriber = (TSubscriber)ctor.Invoke( new object[] { CreateContext<TSubscriber>() } );
            }

            var dispatcherType = Dispatcher.GetType();
            var subscriberType = typeof(IEventSubscriber);
            foreach (var handledEventType in subscriber.HandledEventTypes)
            {
                var registerMethod = dispatcherType.GetMethod("RegisterHandler").MakeGenericMethod(handledEventType);
                var handleMethod = subscriberType.GetMethods().Single(m => m.Name == "Handle");
                var handleDelegate = Delegate.CreateDelegate(typeof(Action<Message>), subscriber, handleMethod);
                registerMethod.Invoke(Dispatcher, new object[] { handleDelegate });
            }

            EventSubscribers.Add( typeof(TSubscriber), subscriber );
            return this;
        }
        
        public TSubscriber ResolveSubscriber<TSubscriber>() where TSubscriber : IEventSubscriber
        {
            return (TSubscriber) EventSubscribers[typeof(TSubscriber)];
        }
        
        public void SendCommand(DomainCommand command)
        {
            CommandBus.Send( command );
        }
    }
}
