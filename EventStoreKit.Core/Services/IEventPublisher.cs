using EventStoreKit.Messages;

namespace EventStoreKit.Services
{
    public interface IEventPublisher
    {
        void Publish<TEvent>( TEvent @event ) where TEvent : Message;
        void Publish( Message @event );
    }
}