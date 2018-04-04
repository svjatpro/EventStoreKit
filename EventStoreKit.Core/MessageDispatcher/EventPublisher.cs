
using EventStoreKit.Utility;

namespace EventStoreKit.Core
{
    public class EventPublisher<TBasic> : IEventPublisher<TBasic> where TBasic : class
    {
        #region Private fields

        private readonly IMessageDispatcher<TBasic> Dispatcher;

        #endregion

        public EventPublisher( IMessageDispatcher<TBasic> dispatcher )
        {
            Dispatcher = dispatcher.CheckNull( nameof(dispatcher) );
        }
        
        public void Publish<TEvent>( TEvent @event ) where TEvent : TBasic
        {
            Dispatcher.Dispatch( @event );
        }
    }
}