using System.Collections.Concurrent;
using System.Diagnostics;
using EventStoreKit.NEventStore.Projections;
using NEventStore.Domain;
using NEventStore.Domain.Core;
using NEventStore.Domain.Persistence;

namespace EventStoreKit.Tests.Shared;

// Shared test domain used across the fixtures.

internal record CounterIncremented(Guid Id, int By);
internal record Unrelated(Guid Id);

// Consumer-side base interface (NOT a library type) — its id is resolved via MapCommandStream.
internal interface ICounterCommand
{
    Guid Id { get; }
}

internal record IncrementCommand(Guid Id, int By, int? ExpectedVersion = null) : ICounterCommand;

// POCO aggregate: convention Apply, command method returns the event.
internal class PocoCounter : IAggregateCommandHandler<IncrementCommand>
{
    public Guid Id { get; private set; }
    public int Value { get; private set; }
    public void Apply(CounterIncremented e) { Id = e.Id; Value += e.By; }
    public object Handle(IncrementCommand cmd) => new CounterIncremented(cmd.Id, cmd.By);
}

// CommonDomain aggregate: same behaviour, registered identically.
internal class CommonDomainCounter : AggregateBase, IAggregateCommandHandler<IncrementCommand>
{
    public int Value { get; private set; }
    public object Handle(IncrementCommand cmd)
    {
        var e = new CounterIncremented(cmd.Id, cmd.By);
        RaiseEvent(e);
        return e;
    }
    private void Apply(CounterIncremented e) => Value += e.By;
}

// Records the events it receives, with a poll-based wait for the async pipeline to settle.
internal class Recorder : EventQueueSubscriber, IEventHandler<CounterIncremented>
{
    private readonly ConcurrentBag<CounterIncremented> processed = [];
    public IReadOnlyCollection<CounterIncremented> Processed => processed;

    public void Handle(CounterIncremented e) => processed.Add(e);

    public bool WaitFor(int count, int timeoutMs = 1000)
    {
        var sw = Stopwatch.StartNew();
        while (processed.Count < count && sw.ElapsedMilliseconds < timeoutMs)
        {
            Thread.Sleep(10);
        }
        return processed.Count >= count;
    }
}

internal class NoSagas : IConstructSagas
{
    public ISaga Build(Type type, string id) => throw new NotSupportedException();
}
