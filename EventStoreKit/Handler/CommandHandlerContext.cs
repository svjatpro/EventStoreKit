
using EventStoreKit.Aggregates;

namespace EventStoreKit.Handler
{
    public class CommandHandlerContext
    {
    }
    public class CommandHandlerContext<TEntity> : CommandHandlerContext
        where TEntity : ITrackableAggregate
    {
        public TEntity Entity { get; set; }
    }
}