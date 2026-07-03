using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace EventStoreKit.NEventStore.Projections;

public abstract class EventQueueSubscriber : IEventSubscriber
{
    #region internall classes

    public class EventInfo
    {
        public object Event = null!;
    }

    #endregion

    #region Private fields

    private readonly BlockingCollection<EventInfo> MessageQueue;
    private readonly Dictionary<Type, List<Action<object>>> Handlers;

    // Set by the store on registration; used to log handler exceptions (otherwise swallowed).
    public ILogger? Logger { get; set; }

    // Opt-in concurrency retry for this subscriber's event handlers ([RetryPolicy] on the subclass);
    // 1 = no retry (conflict is logged and swallowed, as before).
    private readonly int RetryAttempts;

    // Raised when this subscriber dequeues a SequenceMarkerEvent. Carries (subscriber, identity)
    // so the EventSequence service can route the completion back to the right wait-session.
    public event Action<EventQueueSubscriber, Guid>? SequenceFinished;

    #endregion

    #region Private methods

    private Action<object> CreateHandler<TEvent>() where TEvent : class
    {
        var handler = (IEventHandler<TEvent>)this;
        return e => handler.Handle((TEvent)e);
    }
        
    private void ProcessMessages( EventInfo message )
    {
        var @event = message.Event;
        var msgType = @event.GetType();

        // The marker is processed as a side-channel signal — fired after the queue position
        // it occupies, so any earlier event has already been processed by this subscriber.
        if ( @event is SequenceMarkerEvent marker )
        {
            try
            {
                SequenceFinished?.Invoke( this, marker.Identity );
            }
            catch ( Exception ex )
            {
                Logger?.LogError( ex, "Error in SequenceFinished handler in {Subscriber}", GetType().Name );
            }
            return;
        }

        if ( !Handlers.TryGetValue(msgType, out var handlers) )
        {
            return;
        }

        for ( var attempt = 1; ; attempt++ )
        {
            try
            {
                foreach ( var handler in handlers )
                {
                    handler(@event);
                }
                return;
            }
            catch (global::NEventStore.ConcurrencyException) when (attempt < RetryAttempts)
            {
                // [RetryPolicy] on the subscriber: re-run the handler (which reloads its saga/state)
                // so its writes / dispatched commands land cleanly on the next attempt.
                Logger?.LogWarning(
                    "Concurrency conflict handling {EventType} in {Subscriber}, retry {Attempt}/{Max}",
                    msgType.Name, GetType().Name, attempt, RetryAttempts);
            }
            catch (Exception ex)
            {
                // log and continue processing other messages (don't kill the polling loop)
                if (Logger != null)
                {
                    Logger.LogError(ex, "Error handling {EventType} in {Subscriber}", msgType.Name, GetType().Name);
                }
                else
                {
                    Console.Error.WriteLine($"[{GetType().Name}] error handling {msgType.Name}: {ex}");
                }
                return;
            }
        }
    }

    #endregion

    #region Protected methods

    protected void Register<TEvent>(Action<TEvent> action, bool singleAction = true) where TEvent : class
    {
        Register(typeof(TEvent), DelegateAdjuster.CastArgument<object, TEvent>(action), singleAction );
    }
    protected void Register(Type eventType, Action<object> action, bool singleAction = true)
    {
        if ( Handlers.TryGetValue( eventType, out var handler ) )
        {
            if ( singleAction && handler.Count > 1 )
            {
                throw new InvalidOperationException( $"Event type '{eventType.Name}' already registered." );
            }
            handler.Add( action );
        }
        else
        {
            Handlers.Add( eventType, [action] );
        }
    }

    protected void Handle<TEvent>( TEvent e, bool isRebuild ) where TEvent : class
    {
        var eventType = e.GetType();
        if( !Handlers.ContainsKey( eventType ) )
            return;
        MessageQueue.Add( new EventInfo{ Event = e } );
    }

    #endregion

    protected EventQueueSubscriber()
    {
        RetryAttempts = GetType().GetCustomAttribute<RetryPolicyAttribute>()?.MaxAttempts ?? 1;

        Handlers = new Dictionary<Type, List<Action<object>>>();

        MessageQueue = new BlockingCollection<EventInfo>();
        MessageQueue.GetConsumingEnumerable()
            .ToObservable( new NewThreadScheduler( a => new Thread(a){ IsBackground = true }) )// IScheduler from scope => ctor
            .Subscribe( ProcessMessages );

        var handlerTypes = GetType()
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

        foreach (var handlerType in handlerTypes)
        {
            var eventType = handlerType.GetGenericArguments()[0];

            var handleMethod = handlerType.GetMethod(nameof(IEventHandler<object>.Handle))!;
            Action<object> handler = msg =>
            {
                try
                {
                    handleMethod.Invoke(this, [msg]);
                }
                catch (TargetInvocationException tie) when (tie.InnerException != null)
                {
                    // Unwrap the reflection wrapper so ProcessMessages sees the real exception — its type
                    // (e.g. ConcurrencyException for the [RetryPolicy] retry), message and stack trace.
                    ExceptionDispatchInfo.Throw(tie.InnerException);
                }
            };
            Register(eventType, handler!, true);
        }

        // Marker is dispatched to every subscriber by the EventSequence service. Registering a
        // no-op entry makes Handle() enqueue it and exposes the type via HandledEventTypes so
        // AddEventsSubscriber wires the dispatcher route. ProcessMessages handles markers via
        // the SequenceFinished event, not via this no-op.
        Register<SequenceMarkerEvent>( _ => { } );
    }

    public void HandleEvent( object @event )
    {
        Handle( @event, false );
    }

    // Reset in-memory state to match a store CleanUp (purge). The base drains any queued-but-unprocessed
    // events so a stale event can't repopulate state right after the reset; subscribers that keep their
    // own in-memory projections/registers override this, call base.Reset(), then clear them.
    public virtual void Reset()
    {
        while ( MessageQueue.TryTake( out _ ) )
        {
        }
    }

    // Direct-enqueue a marker into this subscriber's own queue. Bypasses the event store entirely
    // (v1.2 model): the marker is just a side-channel signal pushed behind whatever events polling
    // / the PostCommit hook have already delivered. When the marker is dequeued, every prior event
    // in this subscriber's queue has been processed.
    public void EnqueueMarker( Guid identity )
    {
        MessageQueue.Add( new EventInfo { Event = new SequenceMarkerEvent { Identity = identity } } );
    }

    public IEnumerable<Type> HandledEventTypes => Handlers.Keys;
}
