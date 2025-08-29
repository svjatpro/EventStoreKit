using System.Data.Common;
using System.Reflection;
using EventStoreKit.NEventStore.Projections;
using NEventStore;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Serialization.Json;
using NEventStore.PollingClient;
using Npgsql;

namespace EventStoreKit.NEventStore;

public class NEventStoreKit : IEventStoreKit, IEventStoreKitServiceBuilder
{
    private bool Initialized;
    private MessageDispatcher Dispatcher;
    private PollingClient2 PollingClient { get; set; }

    public IStoreEvents Store { get; set; }
    
    public IQueryEventsStore Events { get; private set; }
    public ICommandSender Commands { get; private set; }

    #region EventStoreKitServiceBuilder Members

    private Wireup InitializeWireUp(string connectionString)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

        var wireUp = Wireup.Init()
            .UsingSqlPersistence(
                DbProviderFactories.GetFactory("Npgsql"),
                connectionString)
            .WithDialect(new PostgreNpgsql6Dialect())
            .InitializeStorageEngine()
            .UsingJsonSerialization();
            //.EnlistInAmbientTransaction();
            //.Compress()

        return wireUp;
    }

    private PollingClient2.HandlingResult HandlePublishedEvent(ICommit commit)
    {
        foreach (var @event in commit.Events)
        {
            Dispatcher.Dispatch(@event);
            Dispatcher.Dispatch(@event.Body);
        }
        return PollingClient2.HandlingResult.MoveToNext;
    }

    #endregion

    public NEventStoreKit()
    {
        Dispatcher = new MessageDispatcher();
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand>(
        Func<TCommand, (Guid streamId, object data)> handler)
        where TCommand : class
    {
        Dispatcher.RegisterExclusiveHandler<TCommand>(cmd =>
        {
            var @event = handler(cmd);
            Events.Append(@event.streamId, @event.data);
        });
        return this;
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TAggregate, Guid, Func<TCommand, object>> handler)
        where TCommand : class
        where TAggregate : class, new()
    {
        Dispatcher.RegisterExclusiveHandler<TCommand>(cmd =>
        {
            var id = Guid.NewGuid();
            var aggregate = new TAggregate();
            var @event = handler(aggregate, id)(cmd);
            Events.Append(id, @event);
        });
        return this;
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TCommand, Guid> streamIdGetter,
        Func<TAggregate, Func<TCommand, object>> handler)
        where TCommand : class
        where TAggregate : class, new()
    {
        Dispatcher.RegisterExclusiveHandler<TCommand>(cmd =>
        {
            var id = streamIdGetter(cmd);
            var aggregate = Aggregate<TAggregate>(id);
            var @event = handler(aggregate)(cmd);
            Events.Append(id, @event);
        });
        return this;
    }

    private TAggregate Aggregate<TAggregate>(Guid id) where TAggregate : class, new()
    {
        var aggregate = new TAggregate();
        var applyMethods = aggregate.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m =>
                m.Name.StartsWith("Apply") &&
                m.GetParameters().Length == 1 &&
                m.ReturnType == typeof(void))
            .ToDictionary(m => m.GetParameters()[0].ParameterType);
        var stream = Store.OpenStream(id);
        foreach (var commit in stream.CommittedEvents)
        {
            var @event = commit.Body;
            if(applyMethods.TryGetValue(@event.GetType(), out var applyMethod))
            {
                applyMethod.Invoke(aggregate, [@event]);
            }
        }

        return aggregate;
    }
    public IEventStoreKitServiceBuilder AddAggregate<TAggregate>()
        where TAggregate : class, new()
    {
        var handlerInterface = typeof(ICommandHandler<>);
        var handlerTypes = typeof(TAggregate)
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
            .ToList();
        foreach (var handlerType in handlerTypes)
        {
            var cmdType = handlerType.GetGenericArguments()[0];
            var registerMethod = typeof(MessageDispatcher)
                .GetMethod(nameof(MessageDispatcher.RegisterExclusiveHandler))!
                .MakeGenericMethod(cmdType);

            registerMethod.Invoke(Dispatcher,
                [(Delegate)Activator.CreateInstance(
                    typeof(Func<,>).MakeGenericType(cmdType, typeof(object)),
                    (Func<object, object>)(cmd =>
                        handlerType.GetMethod("Handle")!.Invoke(new TAggregate(), [cmd]))
                )]);
        }

        return this;
    }

    public IEventStoreKitServiceBuilder AddEventsSubscriber<TSubscriber>(TSubscriber subscriber)
        where TSubscriber : IEventSubscriber
    {
        foreach (var eventType in subscriber.HandledEventTypes)
        {
            var handlerType = typeof(Action<>).MakeGenericType(eventType);
            var handler = Delegate.CreateDelegate(
                handlerType,
                subscriber,
                subscriber.GetType().GetMethod(nameof(IEventSubscriber.HandleEvent), [typeof(object)])!);

            var registerMethod = typeof(MessageDispatcher)
                .GetMethod(nameof(MessageDispatcher.RegisterHandler))!
                .MakeGenericMethod(eventType);
            registerMethod.Invoke(Dispatcher, [handler]);
        }

        return this;
    }

    public IEventStoreKit Initialize(string connectionString)
    {
        if(Initialized) throw new InvalidOperationException("EventStoreKit is already initialized.");

        var wireUp = InitializeWireUp(connectionString);
        Store = wireUp.Build();

        Events = new NQueryEventsStore(Store);
        Commands = Dispatcher;

        PollingClient = new PollingClient2(Store.Advanced, HandlePublishedEvent);
        PollingClient.StartFrom();

        Initialized = true;
         
        return this;
    }

    public void Dispose()
    {
        PollingClient.Stop();
        PollingClient.Dispose();
        Store.Dispose();
    }
}
