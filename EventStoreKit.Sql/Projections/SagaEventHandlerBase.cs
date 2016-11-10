using System.Reactive.Concurrency;
using CommonDomain.Persistence;
using EventStoreKit.Logging;
using EventStoreKit.Projections;

namespace EventStoreKit.Sql.Projections
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
