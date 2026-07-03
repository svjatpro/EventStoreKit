using NEventStore;

namespace EventStoreKit.NEventStore;

public class NQueryEventsStore : IQueryEventsStore
{
    public IStoreEvents Store { get; }

    // Reports each commit's checkpoint token so the store can track the current command scope's
    // max-appended token (used for the under-the-hood post-write barrier).
    private readonly Action<long>? OnAppended;

    public NQueryEventsStore(IStoreEvents store, Action<long>? onAppended = null)
    {
        Store = store;
        OnAppended = onAppended;
    }

    public long Append<T>(Guid streamId, T @event)
    {
        using var stream = Store.OpenStream(streamId);
        stream.Add(new EventMessage { Body = @event! });
        return Record(stream.CommitChanges(Guid.NewGuid()));
    }

    public long Append(Guid streamId, IEnumerable<object> events)
    {
        using var stream = Store.OpenStream(streamId);
        foreach (var @event in events)
        {
            stream.Add(new EventMessage { Body = @event! });
        }
        return Record(stream.CommitChanges(Guid.NewGuid()));
    }

    public long Append(string streamId, IEnumerable<object> events, Action<IDictionary<string, object>>? updateHeaders = null)
    {
        var headers = new Dictionary<string, object>();
        updateHeaders?.Invoke(headers);

        using var stream = Store.OpenStream(streamId);
        foreach (var @event in events)
        {
            stream.Add(new EventMessage { Body = @event! });
        }
        foreach(var header in headers)
        {
            stream.UncommittedHeaders[header.Key] = header.Value;
        }

        return Record(stream.CommitChanges(Guid.NewGuid()));
    }

    public int GetVersion( Guid streamId )
    {
        using var stream = Store.OpenStream( streamId );
        return stream.StreamRevision;
    }

    public IEnumerable<EventMessage> GetAllEvents()
    {
        return Store.Advanced.GetFrom(0)
           .SelectMany(x => x.Events)
           .ToList();
    }

    private long Record(ICommit? commit)
    {
        var token = commit?.CheckpointToken ?? 0;
        OnAppended?.Invoke(token);
        return token;
    }
}
