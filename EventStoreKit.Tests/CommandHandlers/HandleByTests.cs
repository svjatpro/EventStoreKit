using EventStoreKit.Tests.Shared;
using FluentAssertions;
using NEventStore;
using NUnit.Framework;

namespace EventStoreKit.Tests.CommandHandlers;

// store.HandleBy<T>(...) runtime command sugar: produce + append, with retry and expected-version.
[TestFixture]
public class HandleByTests : InMemoryStoreFixture
{
    [Test]
    public void HandleByProducesAndAppends()
    {
        var id = Guid.NewGuid();
        Service.HandleBy<PocoCounter>(id, _ => new CounterIncremented(id, 5));

        Service.Aggregate<PocoCounter>(id).Value.Should().Be(5);
    }

    [Test]
    public void HandleByRetriesTransientConflict()
    {
        var id = Guid.NewGuid();
        var calls = 0;
        Service.HandleBy<PocoCounter>(
            id,
            _ =>
            {
                calls++;
                if (calls == 1)
                {
                    Service.Events.Append(id, new CounterIncremented(id, 100));
                }
                return new CounterIncremented(id, 5);
            },
            retry: 3);

        calls.Should().Be(2);
        Service.Aggregate<PocoCounter>(id).Value.Should().Be(105);
    }

    [Test]
    public void HandleByExpectedVersionMatchCommits()
    {
        var id = Guid.NewGuid();
        Service.Events.Append(id, new CounterIncremented(id, 1));

        Service.HandleBy<PocoCounter>(id, expectedVersion: 1, _ => new CounterIncremented(id, 5));

        Service.Aggregate<PocoCounter>(id).Value.Should().Be(6);
    }

    [Test]
    public void HandleByExpectedVersionMismatchThrows()
    {
        var id = Guid.NewGuid();
        Service.Events.Append(id, new CounterIncremented(id, 1));

        var act = () => Service.HandleBy<PocoCounter>(id, expectedVersion: 0, _ => new CounterIncremented(id, 5));

        act.Should().Throw<ConcurrencyException>();
        Service.Aggregate<PocoCounter>(id).Value.Should().Be(1);
    }
}
