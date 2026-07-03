using System.Collections.Concurrent;
using EventStoreKit.NEventStore.Projections;
using Microsoft.Extensions.Logging;

namespace EventStoreKit;

// "Leaf-on-stream" sync barrier — v1.2 model. The marker is pushed directly into each target
// subscriber's internal queue (bypassing the event store), arriving behind whatever events the
// subscriber has already received. When every target signals SequenceFinished for the marker's
// identity, the caller's Wait returns: every event in those queues before the marker has been
// processed.
//
// For correctness, the framework also dispatches events synchronously on commit (NEventStore
// IPipelineHook.PostCommit). That way, by the time Wait is called after Events.Append, the new
// events are already in subscriber queues — the marker goes to the tail behind them.
public class EventSequence
{
    private sealed class Session( Guid identity, IEnumerable<EventQueueSubscriber> targets )
    {
        public readonly Guid Identity = identity;
        public readonly HashSet<EventQueueSubscriber> Pending = [..targets];
    }

    private readonly IReadOnlyList<EventQueueSubscriber> AllSubscribers;
    private readonly ILogger? Logger;
    private readonly ConcurrentDictionary<Guid, Session> Sessions = new();
    private readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds( 10 );

    public EventSequence(
        IEnumerable<EventQueueSubscriber> subscribers,
        ILogger? logger = null )
    {
        AllSubscribers = subscribers.ToList();
        Logger = logger;

        foreach ( var sub in AllSubscribers )
        {
            sub.SequenceFinished += OnSubscriberFinished;
        }
    }

    // Block until every targeted subscriber has processed a fresh marker. `targets == null`
    // waits on every registered subscriber. Returns true on completion, false on timeout.
    public bool Wait( IEnumerable<EventQueueSubscriber>? targets = null, TimeSpan? timeout = null )
    {
        var subs = ( targets ?? AllSubscribers ).ToList();
        if ( subs.Count == 0 )
        {
            return true;
        }

        var id = Guid.NewGuid();
        var session = new Session( id, subs );
        Sessions[id] = session;

        lock ( session )
        {
            // Push marker directly into each target's queue — never stored.
            foreach ( var sub in subs )
            {
                sub.EnqueueMarker( id );
            }
            var completed = Monitor.Wait( session, timeout ?? DefaultTimeout );
            if ( !completed )
            {
                Sessions.TryRemove( id, out _ );
                var pending = string.Join( ", ", session.Pending.Select( s => s.GetType().Name ) );
                Logger?.LogWarning( "EventSequence.Wait timed out (id={Id}). Still pending: {Pending}", id, pending );
            }
            return completed;
        }
    }

    private void OnSubscriberFinished( EventQueueSubscriber subscriber, Guid identity )
    {
        if ( !Sessions.TryGetValue( identity, out var session ) )
        {
            return;
        }

        lock ( session )
        {
            if ( !session.Pending.Remove( subscriber ) )
            {
                return;
            }
            if ( session.Pending.Count == 0 )
            {
                Sessions.TryRemove( identity, out _ );
                Monitor.Pulse( session );
            }
        }
    }
}
