using System.Reactive.Concurrency;
using CommonDomain.Persistence;
using EventStoreKit.Projections;
using log4net;

namespace EventStoreKit.Sql.Projections
{
    public abstract class SagaEventHandlerBase : EventQueueSubscriber
    {
        protected readonly ISagaRepository Repository;

        protected SagaEventHandlerBase( ILog logger, IScheduler scheduler, ISagaRepository repository )
            : base( logger, scheduler )
        {
            Repository = repository;
        }
    }
}
