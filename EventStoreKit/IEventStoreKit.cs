using EventStoreKit.NEventStore.Projections;
using NEventStore;

namespace EventStoreKit;

public interface IEventStoreKitServiceBuilder
{
    IEventStoreKitServiceBuilder AddCommandHandler<TCommand>(
        Func<TCommand, (Guid streamId, object data)> handler)
        where TCommand : class;

    IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TAggregate, Guid, Func<TCommand, object>> handler)
        where TCommand : class
        where TAggregate : class, new();
    IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TCommand, Guid> streamIdGetter,
        Func<TAggregate, Func<TCommand, object>> handler)
        where TCommand : class
        where TAggregate : class, new();

    IEventStoreKitServiceBuilder AddEventsSubscriber<TSubscriber>(TSubscriber subscriber)
        where TSubscriber : IEventSubscriber;

    IEventStoreKitServiceBuilder AddAggregate<TAggregate>()
        where TAggregate : class, new();

    IEventStoreKit Initialize(string connectionString);
}

public interface IEventStoreKit : IDisposable
{
    IQueryEventsStore Events { get; }
    ICommandSender Commands { get; }
}

public interface IQueryEventsStore
{
    void Append<T>(Guid streamId, T @event);
    void Append(Guid streamId, IEnumerable<object> events);
    void Append(Guid streamId, params object[] events) => Append(streamId, events.AsEnumerable());

    IEnumerable<EventMessage> GetAllEvents();
}

public interface ICommandSender
{
    void Send<T>(T command) where T : class;
}

public interface IMessageDispatcher
{
    void RegisterHandler<TMessage>(Action<TMessage> handler) where TMessage : class;
    void RegisterExclusiveHandler<TMessage>(Action<TMessage> handler) where TMessage : class;

    void Dispatch<TMessage>(TMessage? message) where TMessage : class;
}

public interface IDomainCommand
{
    Guid StreamId { get; }
}
public interface ICommandHandler<TCommand> where TCommand : class //IDomainCommand
{
    object Handle(TCommand command);
    Guid GetStreamId(TCommand command);
}
