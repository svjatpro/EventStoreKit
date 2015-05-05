
using EventStoreKit.Messages;

namespace EventStoreKit.CommandBus
{
    public interface ICommandBus
    {
        void Send<TCommand>( TCommand command ) where TCommand : DomainCommand;
    }
}