using System.Reactive.Concurrency;
using CommonDomain.Persistence;
using EventStoreKit.Logging;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Projections
{
    public abstract class SagaEventHandlerBase : EventQueueSubscriber
    {
        protected readonly ISagaRepository Repository;

        protected SagaEventHandlerBase( 
            ILogger logger, 
            IScheduler scheduler,
            IEventStoreConfiguration config,
            ISagaRepository repository )
            : base( logger, scheduler, config )
        {
            Repository = repository;
        }
    }
}
