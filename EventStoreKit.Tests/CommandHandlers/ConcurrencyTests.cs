using EventStoreKit.Tests.Shared;
using FluentAssertions;
using NEventStore;
using NUnit.Framework;

namespace EventStoreKit.Tests.CommandHandlers;

// AddCommandHandler<TCommand, TAggregate> optimistic concurrency: client expected-version (fail-fast)
// and transient server-load retry.
[TestFixture]
public class ConcurrencyTests : InMemoryStoreFixture
{
    [Test]
    public void ExpectedVersionMatchCommits()
    {
        Service.AddCommandHandler<IncrementCommand, PocoCounter>(
            c => c.Id, a => a.Handle, c => c.ExpectedVersion);
        var id = Guid.NewGuid();
        Service.Events.Append(id, new CounterIncremented(id, 1));

        Service.Commands.Send(new IncrementCommand(id, 5, ExpectedVersion: 1));

        Service.Aggregate<PocoCounter>(id).Value.Should().Be(6);
    }

    [Test]
    public void ExpectedVersionMismatchFailsFastWithoutRetrying()
    {
        var calls = 0;
        Service.AddCommandHandler<IncrementCommand, PocoCounter>(
            c => c.Id, a => cmd => { calls++; return a.Handle(cmd); }, c => c.ExpectedVersion);
        var id = Guid.NewGuid();
        Service.Events.Append(id, new CounterIncremented(id, 1));

        var act = () => Service.Commands.Send(new IncrementCommand(id, 5, ExpectedVersion: 0));

        act.Should().Throw<ConcurrencyException>();
        calls.Should().Be(1);
        Service.Aggregate<PocoCounter>(id).Value.Should().Be(1);
    }

    [Test]
    public void CommonDomainExpectedVersionMismatchFailsFast()
    {
        Service.AddCommandHandler<IncrementCommand, CommonDomainCounter>(
            c => c.Id, a => a.Handle, c => c.ExpectedVersion);
        var id = Guid.NewGuid();
        Service.Events.Append(id, new CounterIncremented(id, 1));

        var act = () => Service.Commands.Send(new IncrementCommand(id, 5, ExpectedVersion: 0));

        act.Should().Throw<ConcurrencyException>();
        Service.Aggregate<CommonDomainCounter>(id).Value.Should().Be(1);
    }

    [Test]
    public void TransientConflictIsRetriedUntilItSucceeds()
    {
        var calls = 0;
        Service.AddCommandHandler<IncrementCommand, PocoCounter>(
            c => c.Id,
            a => cmd =>
            {
                calls++;
                if (calls == 1)
                {
                    Service.Events.Append(cmd.Id, new CounterIncremented(cmd.Id, 100));
                }
                return a.Handle(cmd);
            },
            retry: 3);

        var id = Guid.NewGuid();
        Service.Commands.Send(new IncrementCommand(id, 5));

        calls.Should().Be(2);
        Service.Aggregate<PocoCounter>(id).Value.Should().Be(105);
    }

    [Test]
    public void TransientConflictGivesUpAfterExhaustingRetries()
    {
        Service.AddCommandHandler<IncrementCommand, PocoCounter>(
            c => c.Id,
            a => cmd => { Service.Events.Append(cmd.Id, new CounterIncremented(cmd.Id, 1)); return a.Handle(cmd); },
            retry: 2);

        var act = () => Service.Commands.Send(new IncrementCommand(Guid.NewGuid(), 5));

        act.Should().Throw<ConcurrencyException>();
    }

    [Test]
    public void WithoutVersionOrRetryAppendIsUnguarded()
    {
        Service.AddCommandHandler<IncrementCommand, PocoCounter>(c => c.Id, a => a.Handle);
        var id = Guid.NewGuid();
        Service.Commands.Send(new IncrementCommand(id, 5));
        Service.Commands.Send(new IncrementCommand(id, 5));

        Service.Aggregate<PocoCounter>(id).Value.Should().Be(10);
    }
}
