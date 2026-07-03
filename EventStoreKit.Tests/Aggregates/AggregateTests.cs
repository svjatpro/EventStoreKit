using EventStoreKit.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests.Aggregates;

[TestFixture]
public class AggregateTests : InMemoryStoreFixture
{
    [Test]
    public void AggregateFoldsItsEventsInOrder()
    {
        var id = Guid.NewGuid();
        Service.Events.Append(id, new CounterIncremented(id, 2));
        Service.Events.Append(id, new CounterIncremented(id, 3));

        var agg = Service.Aggregate<PocoCounter>(id);
        agg.Id.Should().Be(id);
        agg.Value.Should().Be(5);
    }

    [Test]
    public void AggregateSkipsEventsWithoutAMatchingApply()
    {
        var id = Guid.NewGuid();
        Service.Events.Append(id, new CounterIncremented(id, 2));
        Service.Events.Append(id, new Unrelated(id));

        Service.Aggregate<PocoCounter>(id).Value.Should().Be(2);
    }

    [Test]
    public void AggregateOfUnknownStreamIsAFreshInstance()
    {
        Service.Aggregate<PocoCounter>(Guid.NewGuid()).Value.Should().Be(0);
    }

    [Test]
    public void AggregateSurfacesTheLoadedVersion()
    {
        var id = Guid.NewGuid();
        Service.Events.Append(id, new CounterIncremented(id, 2));
        Service.Events.Append(id, new CounterIncremented(id, 3));

        var agg = Service.Aggregate<PocoCounter>(id, out var version);
        version.Should().Be(2);
        agg.Value.Should().Be(5);
    }

    [Test]
    public void GetVersionReportsTheStreamRevision()
    {
        var id = Guid.NewGuid();
        Service.Events.GetVersion(id).Should().Be(0);

        Service.Events.Append(id, new CounterIncremented(id, 1));
        Service.Events.Append(id, new CounterIncremented(id, 1));

        Service.Events.GetVersion(id).Should().Be(2);
    }
}
