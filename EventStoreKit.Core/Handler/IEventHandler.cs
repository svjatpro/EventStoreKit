
using EventStoreKit.Messages;

namespace EventStoreKit.Handler
{
    public interface IEventHandler
    {
    }
    public interface IEventHandler<TEvent> : IEventHandler
        where TEvent : Message
    {
        void Handle( TEvent message );
    }

    public interface IEventHandlerShort<TEvent> : IEventHandler
        where TEvent : Message
    {
        void Handle( TEvent message );
    }
}