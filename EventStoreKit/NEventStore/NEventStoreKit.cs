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

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand>(Func<TCommand, (Guid streamId, object data)> handler)
        where TCommand : class
    {
        Dispatcher.RegisterExclusiveHandler<TCommand>(cmd =>
        {
            var @event = handler(cmd);
            Events.Append(@event.streamId, @event.data);
        });
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
                subscriber.GetType().GetMethod(nameof(IEventSubscriber.Handle), [eventType])!);

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
}
