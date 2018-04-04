
namespace EventStoreKit.Core
{
    public interface IEventPublisher<TBasic> where TBasic : class
    {
        void Publish<TEvent>( TEvent @event ) where TEvent : TBasic;
    }
}