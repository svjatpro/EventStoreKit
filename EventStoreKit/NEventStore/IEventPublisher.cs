
namespace EventStoreKit.NEventStore;

public interface IEventPublisher
{
    void Publish<TEvent>( TEvent @event );
}