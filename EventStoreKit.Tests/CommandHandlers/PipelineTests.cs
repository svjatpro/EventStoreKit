using EventStoreKit.Tests.Shared;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests.CommandHandlers;

// Full pipeline: command -> aggregate -> event store -> subscriber, for POCO and CommonDomain aggregates.
[TestFixture]
public class PipelineTests : InMemoryStoreFixture
{
    [Test]
    public void CommandFlowsThroughToTheSubscriber()
    {
        var recorder = new Recorder();
        Service.AddEventsSubscriber(recorder);
        Service.AddCommandHandler<IncrementCommand, PocoCounter>(c => c.Id, a => a.Handle);

        var id = Guid.NewGuid();
        Service.Commands.Send(new IncrementCommand(id, 5));
        Service.Commands.Send(new IncrementCommand(id, 2));

        recorder.WaitFor(2).Should().BeTrue();
        recorder.Processed.Sum(e => e.By).Should().Be(7);
        Service.Aggregate<PocoCounter>(id).Value.Should().Be(7);
    }

    [Test]
    public void CommonDomainAggregateRunsThroughTheSamePipeline()
    {
        var recorder = new Recorder();
        Service.AddEventsSubscriber(recorder);
        Service.AddCommandHandler<IncrementCommand, CommonDomainCounter>(c => c.Id, a => a.Handle);

        var id = Guid.NewGuid();
        Service.Commands.Send(new IncrementCommand(id, 5));

        recorder.WaitFor(1).Should().BeTrue();
        recorder.Processed.Single().By.Should().Be(5);
        Service.Aggregate<CommonDomainCounter>(id).Value.Should().Be(5);
    }
}
