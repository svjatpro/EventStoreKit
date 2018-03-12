using EventStoreKit.Messages;

namespace EventStoreKit.Handler
{
    public interface IEventHandlerTransient<TEvent>
        where TEvent : Message
    {
        void Handle( TEvent message );
    }
}