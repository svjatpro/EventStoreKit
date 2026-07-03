using EventStoreKit.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests.CommandHandlers;

// Wholesale AddAggregate<T>(): the aggregate's IAggregateCommandHandler<T> impls are discovered, with
// stream id resolved via MapCommandStream (a consumer base type), for POCO and CommonDomain alike.
[TestFixture]
public class AddAggregateTests : InMemoryStoreFixture
{
    [Test]
    public void AddAggregateResolvesIdViaMappedCommandStreamAndAppends()
    {
        Service.MapCommandStream<ICounterCommand>(c => c.Id);
        Service.AddAggregate<PocoCounter>();
        var id = Guid.NewGuid();

        Service.Commands.Send(new IncrementCommand(id, 5));

        Service.Aggregate<PocoCounter>(id).Value.Should().Be(5);
    }

    [Test]
    public void AddAggregateWorksForCommonDomain()
    {
        Service.MapCommandStream<ICounterCommand>(c => c.Id);
        Service.AddAggregate<CommonDomainCounter>();
        var id = Guid.NewGuid();

        Service.Commands.Send(new IncrementCommand(id, 5));

        Service.Aggregate<CommonDomainCounter>(id).Value.Should().Be(5);
    }

    [Test]
    public void AddAggregateThrowsWhenNoIdResolverRegistered()
    {
        Service.AddAggregate<PocoCounter>();

        var act = () => Service.Commands.Send(new IncrementCommand(Guid.NewGuid(), 5));

        act.Should().Throw<InvalidOperationException>();
    }
}
