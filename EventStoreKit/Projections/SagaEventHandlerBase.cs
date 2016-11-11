using System.Reactive.Concurrency;
using CommonDomain.Persistence;
using EventStoreKit.Logging;

namespace EventStoreKit.Projections
{
    public abstract class SagaEventHandlerBase : EventQueueSubscriber
    {
        protected readonly ISagaRepository Repository;

        protected SagaEventHandlerBase( ILogger logger, IScheduler scheduler, ISagaRepository repository )
            : base( logger, scheduler )
        {
            Repository = repository;
        }
    }
}
