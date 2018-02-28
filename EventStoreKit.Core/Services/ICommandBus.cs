
using EventStoreKit.Messages;

namespace EventStoreKit.Services
{
    public interface ICommandBus
    {
        void SendCommand<TCommand>( TCommand command ) where TCommand : DomainCommand;
    }
}