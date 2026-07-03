using EventStoreKit;
using EventStoreKit.NEventStore;
using NUnit.Framework;

namespace EventStoreKit.Tests.Sagas;

// A POCO saga's React (policy) runs live-only. Reloading the saga replays its stream through Apply
// (evolve) but never through React, so old events never re-dispatch. The saga needs no replay flag.
[TestFixture]
public class SagaReplayTests
{
    private record SagaPinged( Guid PersonId );
    private record PingAck( Guid PersonId );

    // No base class, no Apply (stateless): React returns one PingAck command per SagaPinged.
    private class PingSaga
    {
        public IEnumerable<object> React( SagaPinged @event ) => [new PingAck( @event.PersonId )];
    }

    private sealed class RecordingSender : ICommandSender
    {
        public List<object> Sent { get; } = [];

        public void Send<TCommand>( TCommand command ) where TCommand : class => Sent.Add( command );

        public void Send<TCommand, TContext>( TCommand command, TContext context ) where TCommand : class => Sent.Add( command );

        public bool TrySend<TCommand>( TCommand command ) where TCommand : class
        {
            Sent.Add( command );
            return true;
        }

        public bool TrySend<TCommand, TContext>( TCommand command, TContext context ) where TCommand : class
        {
            Sent.Add( command );
            return true;
        }

        public bool TrySend<TCommand, TContext>( TCommand command, out TContext context ) where TCommand : class
        {
            Sent.Add( command );
            context = default!;
            return true;
        }
    }

    [Test]
    public void Replayed_events_do_not_redispatch_commands()
    {
        using var service = new NEventStoreKit( new PocoSagaFactory() );
        service.InitializeInMemory();
        var repository = service.SagaRepository();
        var sender = new RecordingSender();
        const string id = "PingSaga_1";

        // first new event -> one command
        repository.ProcessAndDispatch<PingSaga>( id, new SagaPinged( Guid.NewGuid() ), sender );
        Assert.That( sender.Sent, Has.Count.EqualTo( 1 ) );

        // second new event reloads the saga (replaying the first from its stream via Apply, not React);
        // only the live event reacts, so the count grows by exactly one.
        repository.ProcessAndDispatch<PingSaga>( id, new SagaPinged( Guid.NewGuid() ), sender );
        Assert.That( sender.Sent, Has.Count.EqualTo( 2 ) );
    }
}
