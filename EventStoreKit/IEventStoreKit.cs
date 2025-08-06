using EventStoreKit.NEventStore.Projections;
using NEventStore;

namespace EventStoreKit;

public interface IEventStoreKitServiceBuilder
{
    IEventStoreKitServiceBuilder AddCommandHandler<TCommand>(Func<TCommand, (Guid streamId, object data)> handler)
        where TCommand : class;

    IEventStoreKitServiceBuilder AddEventsSubscriber<TSubscriber>(TSubscriber subscriber)
        where TSubscriber : IEventSubscriber;

    IEventStoreKit Initialize(string connectionString);
}

public interface IEventStoreKit
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

//public interface ICommandHandler<in TCommand> where TCommand : class
//{
//    Task Handle(TCommand command);
//}
