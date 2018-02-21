using CommonDomain.Persistence;
using EventStoreKit.Services;

namespace EventStoreKit.Projections
{
    public abstract class SagaEventHandlerBase : EventQueueSubscriber
    {
        protected readonly ISagaRepository Repository;

        protected SagaEventHandlerBase( 
            IEventStoreSubscriberContext context,
            ISagaRepository repository )
            : base( context )
        {
            Repository = repository;
        }
    }
}
