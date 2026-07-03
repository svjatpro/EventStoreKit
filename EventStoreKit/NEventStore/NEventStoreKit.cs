using System.Reflection;
using System.Runtime.ExceptionServices;
using EventStoreKit.NEventStore.DbProvider;
using EventStoreKit.NEventStore.Projections;
using Microsoft.Extensions.Logging;
using NEventStore;
using NEventStore.Domain;
using NEventStore.Domain.Persistence;
using NEventStore.Domain.Persistence.EventStore;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.PollingClient;
using NEventStore.Serialization.Json;
using Npgsql;
using DbProviderFactory = EventStoreKit.NEventStore.DbProvider.DbProviderFactory;

namespace EventStoreKit.NEventStore;

public class Event
{
    public string StreamId { get; set; }
    public int StreamRevision { get; set; }
    public long CheckpointToken { get; set; }
    public DateTime CommitStamp { get; set; }

    public object Body { get; set; }
    public Dictionary<string, object> Headers { get; set; }

}

public class CommandSenderStub : ICommandSender
{
    public void Send<TCommand>(TCommand command) where TCommand : class
    {
        
    }

    public void Send<TCommand, TContext>(TCommand command, TContext context) where TCommand : class
    {
        
    }

    public bool TrySend<TCommand>(TCommand command) where TCommand : class
    {
        return false;
    }

    public bool TrySend<TCommand, TContext>(TCommand command, TContext context) where TCommand : class
    {
        return false;
    }

    public bool TrySend<TCommand, TContext>(TCommand command, out TContext context) where TCommand : class
    {
        context = default!;
        return false;
    }
}
public class EventStoreKitStub : IEventStoreKit
{
    public void Dispose()
    {
    }

    public IQueryEventsStore Events { get; }
    public ICommandSender Commands { get; } = new CommandSenderStub();
    public Func<ISagaRepository> SagaRepository { get; }
    public TAggregate Aggregate<TAggregate>(Guid id) where TAggregate : class, new()
    {
        throw new NotImplementedException();
    }

    public TAggregate Aggregate<TAggregate>(Guid id, out int version) where TAggregate : class, new()
    {
        throw new NotImplementedException();
    }

    public TSaga AggregateSaga<TSaga>(string id) where TSaga : class, ISaga, new()
    {
        throw new NotImplementedException();
    }

    public bool WaitForSubscribers(IEnumerable<Type>? targets = null, TimeSpan? timeout = null) => true;

    public long HandleBy<TAggregate>(Guid streamId, Func<TAggregate, object?> produce, int retry = 0, IEnumerable<Type>? waitFor = null)
        where TAggregate : class, new() => throw new NotImplementedException();

    public long HandleBy<TAggregate>(Guid streamId, int expectedVersion, Func<TAggregate, object?> produce, IEnumerable<Type>? waitFor = null)
        where TAggregate : class, new() => throw new NotImplementedException();

    public void WaitForProcessed(long token, IEnumerable<Type> targets)
    {
    }

    public void CleanUp()
    {
    }
}
public class NEventStoreKit : IEventStoreKit, IEventStoreKitServiceBuilder
{
    private const string SagaType = "SagaType";

    private readonly IConstructSagas SagaFactory;
    private readonly IDbProviderFactory? ExternalDbProviderFactory;
    private readonly ILoggerFactory? LoggerFactory;
    private bool Initialized;
    private MessageDispatcher Dispatcher;
    private PollingClient2 PollingClient { get; set; }
    private IDbProviderFactory StoreDbProviderFactory;
    private readonly List<EventQueueSubscriber> QueueSubscribers = new();
    private readonly List<(Type CommandBase, Func<object, Guid> Resolve)> DomainCommandIds = new();
    private EventSequence? EventSequence;

    // Phase-1 dispatch position. The polling client is the sole, in-order dispatcher: after it
    // enqueues a commit's events into the subscriber queues it advances this monotonic watermark
    // and pulses waiters. A [WaitFor] command blocks (WaitForEnqueued) until the watermark reaches
    // the max checkpoint it wrote — guaranteeing its events are queued before its phase-2 marker
    // is pushed. Single writer (the poll thread) means the high-water mark is always monotonic, so
    // nothing is ever skipped. (EventStoreKit-v3 candidate: promote to a consumer-offset primitive.)
    private long EnqueuedToken;
    private readonly object EnqueuedLock = new();
    private readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds( 10 );

    // Max checkpoint token appended within the current synchronous command scope. The command wrapper
    // resets it around each dispatched command so a registered barrier waits on exactly that command's
    // writes — no token threading in handlers. Forked work (Task.Run) runs in a fresh async flow, so
    // its appends are excluded (e.g. the background apply sequence).
    private readonly AsyncLocal<long> ScopeAppendMax = new();

    // Command type -> subscribers to wait for after the command's writes — the under-the-hood
    // ".WaitForProcessedBy(...)". Declared at composition via WaitFor<T>; keyed by the bare command
    // type (IntegrationCommand<T,_> is unwrapped to T).
    private readonly Dictionary<Type, Type[]> CommandBarriers = new();

    private void RecordAppend( long token )
    {
        if ( token > ScopeAppendMax.Value )
        {
            ScopeAppendMax.Value = token;
        }
    }

    private static Type LogicalCommandType( Type cmdType ) =>
        cmdType.IsGenericType && cmdType.GetGenericTypeDefinition() == typeof(IntegrationCommand<,>)
            ? cmdType.GetGenericArguments()[0]
            : cmdType;

    // Barrier targets for a command = the [WaitForSubscriber] attribute (if any) plus the WaitFor<T>
    // registry entry. The registry is the layered home for this (handlers stay subscriber-agnostic).
    private IEnumerable<Type>? BarrierTargets( IEnumerable<Type>? attributeTargets, Type cmdType )
    {
        CommandBarriers.TryGetValue( LogicalCommandType( cmdType ), out var registered );
        if ( attributeTargets == null )
        {
            return registered;
        }
        return registered == null ? attributeTargets : attributeTargets.Concat( registered ).Distinct();
    }

    public IEventStoreKitServiceBuilder WaitFor<TCommand>( params Type[] subscribers )
        where TCommand : class
    {
        CommandBarriers[typeof(TCommand)] = subscribers;
        return this;
    }

    public IStoreEvents Store { get; private set; }
    public Func<ISagaRepository> SagaRepository { get; private set; }

    public IQueryEventsStore Events { get; private set; }
    public ICommandSender Commands { get; private set; }

    private Func<IDbProvider> DbFactory => StoreDbProviderFactory.CreateDbProvider;

    #region EventStoreKitServiceBuilder Members

    private Wireup InitializeWireUp(string connectionString)
    {
        //AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        System.Data.Common.DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

        // Dispatch is driven solely by the polling client (HandlePublishedEvent) in strict commit
        // order — one ordered authority, like the old synchronous-on-commit dispatcher. No PostCommit
        // hook: a second in-process dispatch source forced a high-water-mark de-dup, which silently
        // dropped commits when two threads' writes interleaved out of token order.
        var wireUp = Wireup.Init()
            .UsingSqlPersistence(
                System.Data.Common.DbProviderFactories.GetFactory("Npgsql"),
                connectionString)
            .WithDialect(new PostgreNpgsql6Dialect())
            .InitializeStorageEngine()
            .UsingJsonSerialization();
            //.EnlistInAmbientTransaction();
            //.Compress()

        return wireUp;
    }

    private void DispatchCommitEvents(ICommit commit)
    {
        if (commit.Headers.ContainsKey(SagaType)) // saga events go through SagaRepository, not subscribers
        {
            return;
        }
        foreach (var @event in commit.Events)
        {
            Dispatcher.Dispatch(new Event
            {
                Body = @event.Body,
                Headers = @event.Headers,
                StreamId = commit.StreamId,
                StreamRevision = commit.StreamRevision,
                CheckpointToken = commit.CheckpointToken,
                CommitStamp = commit.CommitStamp,
            });
            Dispatcher.Dispatch(@event.Body);
        }
    }

    // Phase-1 watermark advance. Called only from the polling thread, in strict token order, so
    // the high-water mark is monotonic and never skips a commit.
    private void MarkEnqueued(long token)
    {
        lock (EnqueuedLock)
        {
            if (token > EnqueuedToken)
            {
                EnqueuedToken = token;
            }
            Monitor.PulseAll(EnqueuedLock);
        }
    }

    // Phase 1: block until the polling dispatcher has enqueued every commit up to `checkpoint`
    // into the subscriber queues. Returns false on timeout.
    private bool WaitForEnqueued(long checkpoint, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultWaitTimeout);
        lock (EnqueuedLock)
        {
            while (EnqueuedToken < checkpoint)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    LoggerFactory?.CreateLogger<NEventStoreKit>()
                        .LogWarning("WaitForEnqueued timed out at {Enqueued}, waiting for {Checkpoint}", EnqueuedToken, checkpoint);
                    return false;
                }
                Monitor.Wait(EnqueuedLock, remaining);
            }
            return true;
        }
    }

    // Two-phase [WaitFor] barrier. Phase 1 (WaitForEnqueued) blocks until the command's events are
    // in the subscriber queues; phase 2 (WaitForSubscribers) blocks until the named subscribers have
    // processed them. Phase 1 is the precondition that makes phase-2's direct-enqueued marker land
    // behind the real events instead of racing ahead of the async polling dispatch.
    public void WaitForProcessed(long checkpoint, IEnumerable<Type> targets)
    {
        WaitForEnqueued(checkpoint);
        WaitForSubscribers(targets);
    }

    private PollingClient2.HandlingResult HandlePublishedEvent(ICommit commit)
    {
        // Sole dispatcher: enqueue the commit's events into the subscriber queues, in token order.
        DispatchCommitEvents(commit);

        // Phase-1 watermark: events up to this checkpoint are now queued; wake any [WaitFor] waiter.
        MarkEnqueued(commit.CheckpointToken);

        // temporarily! store the last processed checkpoint token (also resumes dispatch on restart).
        // In-memory mode has no checkpoint table.
        if (StoreDbProviderFactory != null)
        {
            DbFactory.Run(db =>
            {
                db.Update<Subscriber>(
                    s => true,
                    s => new Subscriber { LastToken = commit.CheckpointToken });
            });
        }

        return PollingClient2.HandlingResult.MoveToNext;
    }

    #endregion

    public NEventStoreKit( IConstructSagas sagaFactory, IDbProviderFactory? dbProviderFactory = null, ILoggerFactory? loggerFactory = null )
    {
        SagaFactory = sagaFactory;
        ExternalDbProviderFactory = dbProviderFactory;
        LoggerFactory = loggerFactory;
        Dispatcher = new MessageDispatcher();
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommandHandler>( Func<TCommandHandler> handlerFactory )
        where TCommandHandler : class
    {
        var handlerInterface = typeof(ICommandHandler<>);
        var handlerTypes = typeof(TCommandHandler)
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
            .ToList();

        var handler = handlerFactory();

        foreach (var handlerType in handlerTypes)
        {
            var cmdType = handlerType.GetGenericArguments()[0];
            var handleMethod = handlerType.GetMethod("Handle")!;
            var actionType = typeof(Action<>).MakeGenericType(cmdType);
            var waitTargets = ResolveWaitTargets( typeof(TCommandHandler), cmdType, [cmdType] );
            var retryAttempts = ResolveRetryAttempts( typeof(TCommandHandler), cmdType, [cmdType] );

            Action<object> wrapper = cmd =>
            {
                for ( var attempt = 1; ; attempt++ )
                {
                    var outer = ScopeAppendMax.Value;
                    ScopeAppendMax.Value = 0;
                    try
                    {
                        IEnumerable<(Guid, object)> events;
                        try
                        {
                            events = (IEnumerable<(Guid, object)>)handleMethod.Invoke(handler, [cmd])!;
                        }
                        catch (TargetInvocationException tie) when (tie.InnerException != null)
                        {
                            // Surface the underlying exception (e.g. FluentValidation.ValidationException) so
                            // downstream filters recognise its type, preserving its original stack trace — a bare
                            // `throw tie.InnerException` resets the trace to this line, hiding the real failure site.
                            ExceptionDispatchInfo.Throw( tie.InnerException );
                            throw; // unreachable; satisfies definite-assignment of `events`
                        }
                        foreach (var (streamId, ev) in events)
                        {
                            Events.Append(streamId, ev);
                        }
                        // Leaf-on-stream: block until declared subscribers catch up. ScopeAppendMax covers
                        // both returned and self-appended events; targets come from the WaitFor registry
                        // (+ any [WaitForSubscriber]).
                        var scopeMax = ScopeAppendMax.Value;
                        var targets = BarrierTargets( waitTargets, cmdType );
                        if ( scopeMax > 0 && targets != null )
                        {
                            WaitForProcessed( scopeMax, targets );
                        }
                        return;
                    }
                    catch (ConcurrencyException) when (attempt < retryAttempts)
                    {
                        // [RetryPolicy]: re-invoking the handler reloads the aggregate from the latest
                        // stream, so the recomputed events append cleanly on the next attempt.
                        LoggerFactory?.CreateLogger<NEventStoreKit>()
                            .LogWarning("Concurrency conflict on {Command}, retry {Attempt}/{Max}", cmdType.Name, attempt, retryAttempts);
                    }
                    finally
                    {
                        ScopeAppendMax.Value = Math.Max( outer, ScopeAppendMax.Value );
                    }
                }
            };

            var del = Delegate.CreateDelegate(actionType, wrapper.Target!, wrapper.Method);
            var registerMethod = typeof(MessageDispatcher)
                .GetMethods()
                .Single(m =>
                    m.Name == nameof(MessageDispatcher.RegisterExclusiveHandler) &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters()[0].ParameterType.IsGenericType &&
                    m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Action<>))
                .MakeGenericMethod(cmdType);

            registerMethod.Invoke(Dispatcher, [del]);
        }

        // ICommandHandler<TCommand, TContext>: capture the out context and surface it via TrySend(out).
        var outputHandlerTypes = typeof(TCommandHandler)
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))
            .ToList();

        foreach (var handlerType in outputHandlerTypes)
        {
            var cmdType = handlerType.GetGenericArguments()[0];
            var ctxType = handlerType.GetGenericArguments()[1];
            var handleMethod = handlerType.GetMethod("Handle")!;
            // Output handler signatures take (cmd, out ctx); look up the impl by both args.
            var waitTargets = ResolveWaitTargets( typeof(TCommandHandler), cmdType, [cmdType, ctxType.MakeByRefType()] );
            var retryAttempts = ResolveRetryAttempts( typeof(TCommandHandler), cmdType, [cmdType, ctxType.MakeByRefType()] );

            Dispatcher.RegisterOutputHandler(cmdType, cmd =>
            {
                for ( var attempt = 1; ; attempt++ )
                {
                    var outer = ScopeAppendMax.Value;
                    ScopeAppendMax.Value = 0;
                    try
                    {
                        var args = new object?[] { cmd, null };
                        IEnumerable<(Guid, object)> events;
                        try
                        {
                            events = (IEnumerable<(Guid, object)>)handleMethod.Invoke(handler, args)!;
                        }
                        catch (TargetInvocationException tie) when (tie.InnerException != null)
                        {
                            // Surface the underlying exception with its original stack trace (a bare
                            // `throw tie.InnerException` resets the trace to this line, hiding the real failure site).
                            ExceptionDispatchInfo.Throw( tie.InnerException );
                            throw; // unreachable; satisfies definite-assignment of `args[1]`
                        }
                        foreach (var (streamId, ev) in events)
                        {
                            Events.Append(streamId, ev);
                        }
                        var scopeMax = ScopeAppendMax.Value;
                        var targets = BarrierTargets( waitTargets, cmdType );
                        if ( scopeMax > 0 && targets != null )
                        {
                            WaitForProcessed( scopeMax, targets );
                        }
                        return args[1];
                    }
                    catch (ConcurrencyException) when (attempt < retryAttempts)
                    {
                        LoggerFactory?.CreateLogger<NEventStoreKit>()
                            .LogWarning("Concurrency conflict on {Command}, retry {Attempt}/{Max}", cmdType.Name, attempt, retryAttempts);
                    }
                    finally
                    {
                        ScopeAppendMax.Value = Math.Max( outer, ScopeAppendMax.Value );
                    }
                }
            });
        }

        // ICommandFilter<TCommand>: opt-out gate consulted by TrySend (legacy<->new transition).
        var filterTypes = typeof(TCommandHandler)
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandFilter<>))
            .ToList();

        foreach (var filterType in filterTypes)
        {
            var cmdType = filterType.GetGenericArguments()[0];
            var canHandleMethod = filterType.GetMethod(nameof(ICommandFilter<object>.CanHandle))!;

            Dispatcher.RegisterFilter(cmdType, cmd => (bool)canHandleMethod.Invoke(handler, [cmd])!);
        }

        return this;
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommandHandler>()
        where TCommandHandler : class, new()
    {
        AddCommandHandler( () => new TCommandHandler() );
        return this;
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand>(
        Func<TCommand, IList<(Guid streamId, object data)>> handler)
        where TCommand : class
    {
        Dispatcher.RegisterExclusiveHandler<TCommand>(cmd =>
        {
            var events = handler(cmd);
            foreach (var (streamId, ev) in events) // todo: group by stream, add batch per stream
            {
                Events.Append(streamId, ev);
            }
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

            if (@event is IEnumerable<object> events)
            {
                Events.Append(id, events.ToArray());
            }
            else
            {
                Events.Append(id, @event);
            }
        });
        return this;
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TCommand, Guid> streamIdGetter,
        Func<TAggregate, Func<TCommand, object>> handler,
        int retry = 0 )
        where TCommand : class
        where TAggregate : class, new()
        => RegisterAggregateCommand( streamIdGetter, handler, expectedVersion: null, retry );

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TCommand, Guid> streamIdGetter,
        Func<TAggregate, Func<TCommand, object>> handler,
        Func<TCommand, int?> expectedVersion )
        where TCommand : class
        where TAggregate : class, new()
        => RegisterAggregateCommand( streamIdGetter, handler, expectedVersion, retry: 0 );

    private IEventStoreKitServiceBuilder RegisterAggregateCommand<TCommand, TAggregate>(
        Func<TCommand, Guid> streamIdGetter,
        Func<TAggregate, Func<TCommand, object>> handler,
        Func<TCommand, int?>? expectedVersion,
        int retry )
        where TCommand : class
        where TAggregate : class, new()
    {
        // [WaitFor] can live on the aggregate's Handle(TCommand) method or on TCommand itself.
        var waitTargets = ResolveWaitTargets( typeof(TAggregate), typeof(TCommand), [typeof(TCommand)] );

        Dispatcher.RegisterExclusiveHandler<TCommand>( cmd =>
        {
            var id = streamIdGetter( cmd );

            // Client-supplied expected version wins over retry: when present, the append asserts the
            // stream is still at that version and a mismatch fails fast (the value is baked into the
            // command, so reloading could never make it match). When absent, retry >= 1 enables
            // server-load OCC for transient races (the handler is pure over aggregate state, so
            // re-running is safe); retry == 0 is the legacy unguarded append. ValidationException is
            // never retried.
            var expected = expectedVersion?.Invoke( cmd );

            for ( var attempt = 1; ; attempt++ )
            {
                var aggregate = LoadAggregate<TAggregate>( id, out var loadedVersion );
                var produced = handler( aggregate )( cmd );
                var events = produced is IEnumerable<object> many ? many.ToArray() : new[] { produced };

                try
                {
                    long maxToken;
                    if ( expected.HasValue )
                    {
                        maxToken = AppendExpectingVersion( id, expected.Value, events );
                    }
                    else if ( retry > 0 )
                    {
                        maxToken = AppendExpectingVersion( id, loadedVersion, events );
                    }
                    else
                    {
                        maxToken = Events.Append( id, events );
                    }
                    if ( waitTargets != null )
                    {
                        WaitForProcessed( maxToken, waitTargets );
                    }
                    return;
                }
                catch ( ConcurrencyException ) when ( !expected.HasValue && attempt <= retry )
                {
                    LoggerFactory?.CreateLogger<NEventStoreKit>()
                        .LogWarning( "OCC conflict on {Command} (stream {Stream}), retry {Attempt}/{Max}", typeof(TCommand).Name, id, attempt, retry );
                }
            }
        } );
        return this;
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TAggregate, Func<TCommand, object>> handler)
        where TCommand : class
        where TAggregate : class, new()
    {
        throw new NotImplementedException();
    }

    public IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
        Func<TAggregate, Func<TCommand, Guid, object>> handler)
        where TCommand : class
        where TAggregate : class, new()
    {
        // Output handler: generates the entity id (unless supplied) and returns it for TrySend(out).
        Dispatcher.RegisterOutputHandler(typeof(IntegrationCommand<TCommand, Guid>), cmdObj =>
        {
            var cmd = (IntegrationCommand<TCommand, Guid>)cmdObj;
            var id = cmd.Context == Guid.Empty ? Guid.NewGuid() : cmd.Context;
            var aggregate = new TAggregate();
            var @event = handler(aggregate)(cmd.Command, id);

            if (@event is IEnumerable<object> events)
            {
                Events.Append(id, events.ToArray());
            }
            else
            {
                Events.Append(id, @event);
            }

            return id;
        });
        return this;
    }

    //public IEventStoreKitServiceBuilder AddCommandHandler<TCommand, TAggregate>(
    //    Func<TAggregate, Func<TCommand, Guid, object>> handler)
    //    where TCommand : class
    //    where TAggregate : class, new()
    //{
    //    Dispatcher.RegisterExclusiveHandler<TCommand>((cmd) =>
    //    {
    //        var id = Guid.NewGuid();
    //        var aggregate = new TAggregate();
    //        var @event = handler(aggregate)(cmd, id);

    //        if (@event is IEnumerable<object> events)
    //            Events.Append(id, events.ToArray());
    //        else
    //            Events.Append(id, @event);
    //    });
    //    return this;
    //}

    public TAggregate Aggregate<TAggregate>( Guid id ) where TAggregate : class, new()
    {
        return LoadAggregate<TAggregate>( id, out _ );
    }

    public TAggregate Aggregate<TAggregate>( Guid id, out int version ) where TAggregate : class, new()
    {
        return LoadAggregate<TAggregate>( id, out version );
    }

    // Hydrate the aggregate by replaying its stream, and surface the loaded version (stream revision)
    // for expected-version OCC. Both aggregate flavours hydrate transparently — registration and
    // command handling are identical for either:
    //   - CommonDomain (NEventStore.Domain.IAggregate): each event routes through the aggregate's own
    //     ApplyEvent (its convention router -> protected Apply(T));
    //   - POCO: reflect public void Apply(T) methods and invoke the matching one (events without an
    //     Apply are skipped, no diagnostic yet).
    private TAggregate LoadAggregate<TAggregate>( Guid id, out int version ) where TAggregate : class, new()
    {
        var aggregate = new TAggregate();
        var stream = Store.OpenStream( id );

        if ( aggregate is IAggregate commonDomain )
        {
            foreach ( var commit in stream.CommittedEvents )
            {
                commonDomain.ApplyEvent( commit.Body );
            }
        }
        else
        {
            var applyMethods = aggregate.GetType()
                .GetMethods( BindingFlags.Public | BindingFlags.Instance )
                .Where( m =>
                    m.Name.StartsWith( "Apply" ) &&
                    m.GetParameters().Length == 1 &&
                    m.ReturnType == typeof(void) )
                .ToDictionary( m => m.GetParameters()[0].ParameterType );
            foreach ( var commit in stream.CommittedEvents )
            {
                if ( applyMethods.TryGetValue( commit.Body.GetType(), out var applyMethod ) )
                {
                    applyMethod.Invoke( aggregate, [commit.Body] );
                }
            }
        }

        version = stream.StreamRevision;
        return aggregate;
    }

    // Expected-version OCC append: rejects with ConcurrencyException if the stream advanced past the
    // version the aggregate was loaded at (a write landed in the load->append window); CommitChanges
    // additionally guards the open->commit window via the (StreamId, CommitSequence) constraint.
    private long AppendExpectingVersion( Guid streamId, int expectedVersion, IEnumerable<object> events )
    {
        using var stream = Store.OpenStream( streamId );
        if ( stream.StreamRevision != expectedVersion )
        {
            throw new ConcurrencyException(
                $"Stream {streamId} is at revision {stream.StreamRevision}, expected {expectedVersion}." );
        }
        foreach ( var @event in events )
        {
            stream.Add( new EventMessage { Body = @event! } );
        }
        var commit = stream.CommitChanges( Guid.NewGuid() );
        var token = commit?.CheckpointToken ?? 0;
        RecordAppend( token );
        return token;
    }

    public long HandleBy<TAggregate>(
        Guid streamId,
        Func<TAggregate, object?> produce,
        int retry = 0,
        IEnumerable<Type>? waitFor = null )
        where TAggregate : class, new()
    {
        for ( var attempt = 1; ; attempt++ )
        {
            try
            {
                var aggregate = Aggregate<TAggregate>( streamId, out var version );
                var @event = produce( aggregate );
                if ( @event == null )
                {
                    return 0;
                }
                var events = @event is IEnumerable<object> many ? many.ToArray() : new[] { @event };
                // retry == 0: unguarded append; retry >= 1: guard the load->append window (server-load OCC).
                var token = retry > 0
                    ? AppendExpectingVersion( streamId, version, events )
                    : Events.Append( streamId, events );
                if ( waitFor != null )
                {
                    WaitForProcessed( token, waitFor );
                }
                return token;
            }
            catch ( ConcurrencyException ) when ( attempt <= retry )
            {
                LoggerFactory?.CreateLogger<NEventStoreKit>()
                    .LogWarning( "Concurrency conflict on stream {Stream}, retry {Attempt}/{Max}", streamId, attempt, retry );
            }
        }
    }

    public long HandleBy<TAggregate>(
        Guid streamId,
        int expectedVersion,
        Func<TAggregate, object?> produce,
        IEnumerable<Type>? waitFor = null )
        where TAggregate : class, new()
    {
        var aggregate = Aggregate<TAggregate>( streamId );
        var @event = produce( aggregate );
        if ( @event == null )
        {
            return 0;
        }
        var events = @event is IEnumerable<object> many ? many.ToArray() : new[] { @event };
        var token = AppendExpectingVersion( streamId, expectedVersion, events );
        if ( waitFor != null )
        {
            WaitForProcessed( token, waitFor );
        }
        return token;
    }

    public IEventStoreKitServiceBuilder MapCommandStream<TCommandBase>( Func<TCommandBase, Guid> idResolver )
    {
        DomainCommandIds.Add( (typeof(TCommandBase), command => idResolver( (TCommandBase)command )) );
        return this;
    }

    // Wholesale aggregate registration: discovers the aggregate's IAggregateCommandHandler<TCommand>
    // interfaces and, per command, resolves the target stream id (via MapCommandStream) and runs the
    // aggregate's Handle as a HandleBy producer (load -> Handle -> append). Works for POCO and
    // CommonDomain aggregates alike (HandleBy hydrates both).
    public IEventStoreKitServiceBuilder AddAggregate<TAggregate>()
        where TAggregate : class, new()
    {
        var handlerTypes = typeof(TAggregate)
            .GetInterfaces()
            .Where( i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAggregateCommandHandler<>) )
            .ToList();

        foreach ( var handlerType in handlerTypes )
        {
            var cmdType = handlerType.GetGenericArguments()[0];
            var handleMethod = handlerType.GetMethod( nameof(IAggregateCommandHandler<object>.Handle) )!;

            Action<object> wrapper = cmd =>
            {
                var id = ResolveAggregateStreamId( cmd );
                HandleBy<TAggregate>( id, agg => handleMethod.Invoke( agg, [cmd] ) );
            };

            var del = Delegate.CreateDelegate( typeof(Action<>).MakeGenericType( cmdType ), wrapper.Target!, wrapper.Method );
            typeof(MessageDispatcher)
                .GetMethod( nameof(MessageDispatcher.RegisterExclusiveHandler) )!
                .MakeGenericMethod( cmdType )
                .Invoke( Dispatcher, [del] );
        }

        return this;
    }

    // Stream id for a command handled by a wholesale-registered aggregate, via the resolver registered
    // with MapCommandStream (exact-type registration wins, else the single assignable base). Guid.Empty
    // means create (a new id is generated).
    private Guid ResolveAggregateStreamId( object command )
    {
        var resolver = DomainCommandIds.FirstOrDefault( e => e.CommandBase == command.GetType() ).Resolve;
        if ( resolver == null )
        {
            var matches = DomainCommandIds.Where( e => e.CommandBase.IsInstanceOfType( command ) ).ToList();
            if ( matches.Count == 0 )
            {
                throw new InvalidOperationException(
                    $"No stream-id resolver registered for command '{command.GetType().Name}'. Register one via MapCommandStream<TBase>(...)." );
            }
            if ( matches.Count > 1 )
            {
                throw new InvalidOperationException(
                    $"Ambiguous stream-id resolver for command '{command.GetType().Name}' — it matches {matches.Count} registered base types. Register a per-command resolver." );
            }
            resolver = matches[0].Resolve;
        }
        var id = resolver( command );
        return id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public TSaga AggregateSaga<TSaga>( string id ) where TSaga : class, ISaga, new ()
    {
        return SagaRepository().GetById<TSaga>(id);

    }

    public IEventStoreKitServiceBuilder AddEventsSubscriber<TSubscriber>(TSubscriber subscriber)
        where TSubscriber : IEventSubscriber
    {
        // give the subscriber a logger so swallowed handler exceptions reach the log
        if (subscriber is EventQueueSubscriber queueSubscriber)
        {
            queueSubscriber.Logger = LoggerFactory?.CreateLogger(subscriber.GetType());
            QueueSubscribers.Add(queueSubscriber);
        }

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

    /// <summary>
    /// Initializes the store against an in-memory event store (tests / standalone use): wires command
    /// dispatch, aggregate hydration and the polling subscriber pipeline, skipping only the persistent
    /// checkpoint table. Command handlers, <see cref="Aggregate{TAggregate}(Guid)"/>, the OCC appends
    /// and event subscribers all work.
    /// </summary>
    public IEventStoreKit InitializeInMemory()
    {
        if ( Initialized )
        {
            throw new InvalidOperationException( "EventStoreKit is already initialized." );
        }

        Store = Wireup.Init()
            .UsingInMemoryPersistence()
            .Build();
        Events = new NQueryEventsStore( Store, RecordAppend );
        Commands = Dispatcher;

        PollingClient = new PollingClient2( Store.Advanced, HandlePublishedEvent, 50 );
        PollingClient.StartFrom( 0 );

        SagaRepository = () => new SagaEventStoreRepository( Store, SagaFactory );

        Initialized = true;

        return this;
    }

    public IEventStoreKit Initialize(string connectionString)
    {
        if(Initialized) throw new InvalidOperationException("EventStoreKit is already initialized.");

        StoreDbProviderFactory = new DbProviderFactory(connectionString);
        
        var wireUp = InitializeWireUp(connectionString);
        Store = wireUp.Build();

        Events = new NQueryEventsStore(Store, RecordAppend);
        Commands = Dispatcher;

        long lastToken = 0;
        DbFactory.Run(db =>
        {
            db.CreateTable<Subscriber>( overwrite: false );
            var subscribers = db.From<Subscriber>().Count();
            if (subscribers <= 0)
                db.Insert(new Subscriber { LastToken = 0 });

            lastToken = db.From<Subscriber>().SingleOrDefault()?.LastToken ?? 0;
        });

        // PollingClient2 default interval is 5000ms which makes write-then-read flows race on the
        // projection pipeline. Lower to 25ms so subscriber pipelines drain almost immediately and the
        // WaitForEnqueued/WaitForSubscribers barriers (bounded by one tick) resolve fast.
        // TODO: replace the fixed interval with an adaptive one (poll fast while a barrier is waiting
        // or just after a write, back off when idle) instead of this hardcoded value.
        PollingClient = new PollingClient2(Store.Advanced, HandlePublishedEvent, 100);
        PollingClient.StartFrom(lastToken);

        SagaRepository = () => new SagaEventStoreRepository( Store, SagaFactory );

        Initialized = true;
         
        return this;
    }

    // Reads [WaitFor] from `containingType.Handle(paramTypes...)` first, falls back to TCommand
    // itself. Returns null when no attribute is declared, so the framework wrapper can skip
    // calling WaitForSubscribers entirely.
    private static Type[]? ResolveWaitTargets(Type containingType, Type commandType, Type[] paramTypes)
    {
        var method = containingType.GetMethod(
            "Handle",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: paramTypes,
            modifiers: null);
        var attr = method?.GetCustomAttribute<WaitForSubscriberAttribute>()
            ?? commandType.GetCustomAttribute<WaitForSubscriberAttribute>();
        return attr?.SubscriberTypes;
    }

    // Max attempts for a command handler under [RetryPolicy]; 1 (no retry) when unmarked. Looks at the
    // Handle method first, then the handler class, then the command class.
    private static int ResolveRetryAttempts(Type containingType, Type commandType, Type[] paramTypes)
    {
        var method = containingType.GetMethod(
            "Handle",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: paramTypes,
            modifiers: null);
        var attr = method?.GetCustomAttribute<RetryPolicyAttribute>()
            ?? containingType.GetCustomAttribute<RetryPolicyAttribute>()
            ?? commandType.GetCustomAttribute<RetryPolicyAttribute>();
        return attr?.MaxAttempts ?? 1;
    }

    public bool WaitForSubscribers(IEnumerable<Type>? targets = null, TimeSpan? timeout = null)
    {
        // Lazy: EventQueueSubscribers are registered after Initialize, so the sequence service
        // can only be built once registration is done. First call wins.
        if (EventSequence == null)
        {
            EventSequence = new EventSequence(
                QueueSubscribers,
                LoggerFactory?.CreateLogger<EventSequence>());
        }

        IEnumerable<EventQueueSubscriber>? targetSubs = null;
        if (targets != null)
        {
            var typeSet = targets.ToHashSet();
            targetSubs = QueueSubscribers.Where(s => typeSet.Contains(s.GetType())).ToList();
        }
        return EventSequence.Wait(targets: targetSubs, timeout: timeout);
    }

    public void CleanUp()
    {
        // Dummy implementation: truncate every table in every configured schema.
        // current_schema() resolves per-connection because each schema has its own
        // SearchPath-scoped connection string in the factory.
        const string truncateAllInCurrentSchema = @"
            DO $$
            DECLARE tbl record;
            BEGIN
                FOR tbl IN (SELECT tablename FROM pg_tables WHERE schemaname = current_schema()) LOOP
                    EXECUTE 'TRUNCATE TABLE ""' || current_schema() || '"".""' || tbl.tablename || '"" CASCADE';
                END LOOP;
            END $$;";

        var factory = ExternalDbProviderFactory ?? StoreDbProviderFactory;
        foreach (var schema in factory.Schemas)
        {
            using var db = factory.CreateDbProvider(schema);
            db.ExecuteNonQuery(truncateAllInCurrentSchema);
        }

        // Truncating drops all events, but EventQueueSubscribers keep in-memory state for the process
        // lifetime (e.g. OrderApplyHandler's order register) — reset them so a purge leaves no ghost state.
        foreach (var subscriber in QueueSubscribers)
        {
            subscriber.Reset();
        }
    }

    public void Dispose()
    {
        PollingClient.Stop();
        PollingClient.Dispose();
        Store.Dispose();
    }
}
