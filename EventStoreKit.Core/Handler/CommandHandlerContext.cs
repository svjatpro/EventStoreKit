using CommonDomain;

namespace EventStoreKit.Handler
{
    public class CommandHandlerContext
    {
    }
    public class CommandHandlerContext<TEntity> : CommandHandlerContext
        where TEntity : IAggregate
    {
        public TEntity Entity { get; set; }
    }
}