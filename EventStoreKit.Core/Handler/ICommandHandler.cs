using CommonDomain;
using EventStoreKit.Messages;

namespace EventStoreKit.Handler
{
    /// <summary>
    /// Basic command handler interface
    /// </summary>
    public interface ICommandHandler
    {
    }

    /// <summary>
    /// Command handler, implemented by aggregate
    /// </summary>
    public interface ICommandHandler<TCommand> : ICommandHandler
        where TCommand : DomainCommand
    {
        void Handle( TCommand cmd );
    }

    /// <summary>
    /// Command handler, implemented by external handler class
    /// </summary>
    public interface ICommandHandler<TCommand, TEntity> : ICommandHandler
        where TCommand : DomainCommand
        where TEntity : IAggregate
    {
        void Handle( TCommand cmd, CommandHandlerContext<TEntity> context );
    }
    
}