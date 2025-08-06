using NEventStore;

namespace EventStoreKit.NEventStore;

public class NQueryEventsStore : IQueryEventsStore
{
    public IStoreEvents Store { get; }

    public NQueryEventsStore(IStoreEvents store)
    {
        Store = store;
    }

    public void Append<T>(Guid streamId, T @event)
    {
        using var stream = Store.OpenStream(streamId);
        stream.Add(new EventMessage { Body = @event! });
        stream.CommitChanges(Guid.NewGuid());
    }

    public void Append(Guid streamId, IEnumerable<object> events)
    {
        using var stream = Store.OpenStream(streamId);
        foreach (var @event in events)
        {
            stream.Add(new EventMessage { Body = @event! });
        }
        stream.CommitChanges(Guid.NewGuid());
    }

    public IEnumerable<EventMessage> GetAllEvents()
    {
        return Store.Advanced.GetFrom(0)
           .SelectMany(x => x.Events)
           .ToList();
    }
}
