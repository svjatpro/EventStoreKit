
using EventStoreKit.Messages;

namespace EventStoreKit.Services
{
    public interface ICommandBus
    {
        void Send<TCommand>( TCommand command ) where TCommand : DomainCommand;
    }
}