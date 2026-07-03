using EventStoreKit.NEventStore.Projections;
using NEventStore;
using NEventStore.Domain;
using NEventStore.Domain.Persistence;

namespace EventStoreKit;

public interface IEventStoreKitServiceBuilder
{
    IEventStoreKitServiceBuilder AddCommandHandler<TCommandHandler>()
        where TCommandHandler : class, new();
    IEventStoreKitServiceBuilder AddCommandHandler<TCommandHandler>( Func<TCommandHandler> handlerFactory )
        where TCommandHandler : class;

    IEventStoreKitServiceBuilder AddCommandHandler<TCommand>(
        Func<TCommand, IList<(Guid streamId, object data)>> handler)
        where TCommand : class;

    IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TAggregate, Guid, Func<TCommand, object>> handler)
        where TCommand : class
        where TAggregate : class, new();
    /// <summary>
    /// Registers a command handler that loads the aggregate, runs <paramref name="handler"/> and appends
    /// the produced event(s), with transient optimistic concurrency.
    /// </summary>
    /// <param name="streamIdGetter">Resolves the target stream id from the command.</param>
    /// <param name="handler">Given the loaded aggregate, returns the event (or events) the command produces.</param>
    /// <param name="retry">
    /// 0 = unguarded append; &gt;= 1 guards the load-to-append window and reloads + re-handles on a
    /// concurrency conflict, up to this many times. The handler must be idempotent.
    /// </param>
    IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TCommand, Guid> streamIdGetter,
        Func<TAggregate, Func<TCommand, object>> handler,
        int retry = 0 )
        where TCommand : class
        where TAggregate : class, new();

    /// <summary>
    /// Registers a command handler with strict client optimistic concurrency: the append asserts the
    /// stream is still at the version <paramref name="expectedVersion"/> reports, failing fast on a
    /// mismatch (never retried).
    /// </summary>
    /// <param name="streamIdGetter">Resolves the target stream id from the command.</param>
    /// <param name="handler">Given the loaded aggregate, returns the event (or events) the command produces.</param>
    /// <param name="expectedVersion">
    /// The stream version the command was built against; returning <c>null</c> skips the check.
    /// </param>
    IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TCommand, Guid> streamIdGetter,
        Func<TAggregate, Func<TCommand, object>> handler,
        Func<TCommand, int?> expectedVersion )
        where TCommand : class
        where TAggregate : class, new();
    IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TAggregate, Func<TCommand, object>> handler)
        where TCommand : class
        where TAggregate : class, new();

    //IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TContext, TAggregate>(
    //    Func<TAggregate, Func<TCommand, TContext, object>> handler)
    //    where TCommand : class
    //    where TAggregate : class, new();

    IEventStoreKitServiceBuilder AddEventsSubscriber<TSubscriber>(TSubscriber subscriber)
        where TSubscriber : IEventSubscriber;
        
    /// <summary>
    /// Registers how to resolve the target stream id for commands assignable to
    /// <typeparamref name="TCommandBase"/> — a consumer-defined base interface/class, or a specific
    /// command type. Consulted by <see cref="AddAggregate{TAggregate}"/>, so commands need not implement
    /// any library interface. A resolved <see cref="System.Guid.Empty"/> means "create" (a new id is generated).
    /// </summary>
    IEventStoreKitServiceBuilder MapCommandStream<TCommandBase>( Func<TCommandBase, Guid> idResolver );

    IEventStoreKitServiceBuilder AddAggregate<TAggregate>()
        where TAggregate : class, new();

    /// <summary>
    /// Declares which subscribers to wait for (WaitForProcessed) after a command's writes — the
    /// "under-the-hood" read-back barrier, keeping handlers free of subscriber/token concerns.
    /// This is the layered home for what a caller would otherwise write as
    /// <c>Send(cmd).WaitForProcessedBy(...)</c>. Keyed by the bare command type.
    /// </summary>
    IEventStoreKitServiceBuilder WaitFor<TCommand>( params Type[] subscribers ) where TCommand : class;

    IEventStoreKit Initialize(string connectionString);
}

public interface IEventStoreKit : IDisposable
{
    IQueryEventsStore Events { get; }
    ICommandSender Commands { get; }

    Func<ISagaRepository> SagaRepository { get; }

    /// <summary>Loads (hydrates) the aggregate by replaying its stream.</summary>
    TAggregate Aggregate<TAggregate>( Guid id ) where TAggregate : class, new();

    /// <summary>
    /// Loads the aggregate and reports the version (stream revision) it was loaded at — for a manual
    /// load -&gt; decide -&gt; append(expectedVersion) flow.
    /// </summary>
    /// <param name="version">The current stream revision; 0 for a stream with no commits.</param>
    TAggregate Aggregate<TAggregate>( Guid id, out int version ) where TAggregate : class, new();

    TSaga AggregateSaga<TSaga>( string id ) where TSaga : class, ISaga, new();

    // Block until the named subscribers have processed every event committed up to now.
    // Implemented as a "leaf-on-stream" barrier: writes a transient marker and waits for each
    // target to dequeue it. `targets == null` waits for every registered subscriber (rare;
    // prefer naming exact subscriber types). Returns true on completion, false on timeout.
    bool WaitForSubscribers( IEnumerable<Type>? targets = null, TimeSpan? timeout = null );

    /// <summary>
    /// Loads the aggregate, runs <paramref name="produce"/> and appends the result, with transient
    /// optimistic concurrency: on a conflict the aggregate is reloaded and the producer re-run, so
    /// losing an OCC race on a shared stream recovers instead of failing.
    /// </summary>
    /// <param name="produce">
    /// Given the (re)loaded aggregate, returns the event — or an <see cref="IEnumerable{T}"/> of events —
    /// to append, or <c>null</c> for a no-op. Runs on every attempt, so aggregate invariants are
    /// re-checked against current state; a <c>ValidationException</c> it throws is a real rejection and
    /// is not retried.
    /// </param>
    /// <param name="retry">Maximum reload-and-retry attempts on a concurrency conflict (0 = unguarded, no retry).</param>
    /// <param name="waitFor">Subscribers to settle (two-phase barrier) on the resulting commit before returning.</param>
    /// <returns>The commit checkpoint token, or 0 if <paramref name="produce"/> returned null.</returns>
    long HandleBy<TAggregate>( Guid streamId, Func<TAggregate, object?> produce, int retry = 0, IEnumerable<Type>? waitFor = null )
        where TAggregate : class, new();

    /// <summary>
    /// Loads the aggregate, runs <paramref name="produce"/> and appends the result asserting the stream is
    /// still at <paramref name="expectedVersion"/> — strict client optimistic concurrency; a mismatch
    /// fails fast (never retried).
    /// </summary>
    /// <param name="produce">Given the loaded aggregate, returns the event (or events) to append, or <c>null</c> for a no-op.</param>
    /// <param name="waitFor">Subscribers to settle (two-phase barrier) on the resulting commit before returning.</param>
    /// <returns>The commit checkpoint token, or 0 if <paramref name="produce"/> returned null.</returns>
    long HandleBy<TAggregate>( Guid streamId, int expectedVersion, Func<TAggregate, object?> produce, IEnumerable<Type>? waitFor = null )
        where TAggregate : class, new();

    /// <summary>
    /// Two-phase post-write barrier on a specific commit token: blocks until the dispatcher has enqueued
    /// everything up to <paramref name="token"/> and the named <paramref name="targets"/> have processed
    /// past it. Unlike <see cref="WaitForSubscribers"/>, this is race-free for a just-appended token.
    /// </summary>
    void WaitForProcessed( long token, IEnumerable<Type> targets );

    void CleanUp();
}

public interface IQueryEventsStore
{
    // Append returns the global CheckpointToken of the resulting commit (0 if nothing was
    // committed). Callers that need a post-write sync barrier use the max token across their
    // appends as the phase-1 "wait until dispatched" target.
    long Append<T>( Guid streamId, T @event );
    long Append( Guid streamId, IEnumerable<object> events );
    long Append( Guid streamId, params object[] events ) => Append(streamId, events.AsEnumerable() );

    long Append( string streamId, IEnumerable<object> events, Action<IDictionary<string, object>>? updateHeaders = null );

    /// <summary>
    /// Current version (stream revision) of a stream; 0 for a stream with no commits. The read-side
    /// source of the expected version a client sends back for strict optimistic concurrency.
    /// </summary>
    int GetVersion( Guid streamId );

    IEnumerable<EventMessage> GetAllEvents();
}

public interface ICommandSender
{
    void Send<TCommand>( TCommand command ) where TCommand : class;
    void Send<TCommand, TContext>( TCommand command, TContext context ) where TCommand : class;

    bool TrySend<TCommand>( TCommand command ) where TCommand : class;
    bool TrySend<TCommand, TContext>( TCommand command, TContext context ) where TCommand : class;

    // Sends and returns the handler's output context (e.g. generated ids); false if no handler.
    bool TrySend<TCommand, TContext>( TCommand command, out TContext context ) where TCommand : class;
}

public interface IMessageDispatcher
{
    void RegisterHandler<TMessage>( Action<TMessage> handler ) where TMessage : class;
    void RegisterExclusiveHandler<TMessage>( Action<TMessage> handler ) where TMessage : class;

    void Dispatch<TMessage>( TMessage? message ) where TMessage : class;
}

public interface ICommandHandler<in TCommand> where TCommand : class
{
    IList<(Guid, object)> Handle( TCommand command );
}

/// <summary>
/// Implemented by an aggregate to handle a command against itself: returns the produced event (or an
/// <see cref="IEnumerable{T}"/> of events) to append to the aggregate's stream.
/// </summary>
public interface IAggregateCommandHandler<in TCommand> where TCommand : class
{
    object Handle( TCommand command );
}

// Optional companion to ICommandHandler<TCommand>: lets a handler opt out of a command at dispatch time.
// CanHandle returning false makes TrySend(...) return false so the caller can fall through to another
// engine (legacy <-> new transition). Send(...) ignores the filter — caller is asserting ownership.
public interface ICommandFilter<in TCommand> where TCommand : class
{
    bool CanHandle( TCommand command );
}

// Command handler that also returns an output context (e.g. generated ids) via TrySend(out).
public interface ICommandHandler<in TCommand, TContext> where TCommand : class
{
    IList<(Guid, object)> Handle(TCommand command, out TContext context);
}

public record StreamEvents( Guid StreamId, IEnumerable<object> Events )
{
    public static implicit operator StreamEvents( (Guid id, object @event) msg ) => new( msg.id, [msg.@event] );
    public static StreamEvents Empty => new( Guid.Empty, [] );
}

public interface ICommandHandlerAsync<in TCommand> where TCommand : class
{
    IAsyncEnumerable<StreamEvents> HandleAsync( TCommand command );
}

public class IntegrationCommand
{
    public static IntegrationCommand<TCommand, TContext> For<TCommand, TContext>( TCommand command, TContext context )
    {
        return new IntegrationCommand<TCommand, TContext>( command, context );
    }
}
public class IntegrationCommand<TCommand, TContext>( TCommand command, TContext context )
    : IntegrationCommand
{
    public TCommand Command { get; set; } = command;
    public TContext Context { get; set; } = context;
}
