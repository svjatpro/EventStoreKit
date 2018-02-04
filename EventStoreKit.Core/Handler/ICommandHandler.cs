using EventStoreKit.Aggregates;
using EventStoreKit.Messages;

namespace EventStoreKit.Handler
{
    public interface ICommandHandler
    {
    }
    
    public interface ICommandHandler<TCommand, TEntity> : ICommandHandler
        where TCommand : DomainCommand
        where TEntity : ITrackableAggregate
    {
        void Handle( TCommand cmd, CommandHandlerContext<TEntity> context );
    }

    //public interface ICommandHandler<TCommand> where TCommand : DomainCommand
    //{
    //    void Handle( TCommand cmd );
    //}
}