using NEventStore.Domain.Persistence;

namespace EventStoreKit.NEventStore.Projections;

// Base for event subscribers that drive a saga
public abstract class SagaEventHandlerBase( Func<ISagaRepository> sagaRepository ) : EventQueueSubscriber
{
    protected Func<ISagaRepository> SagaRepository { get; } = sagaRepository;
}
