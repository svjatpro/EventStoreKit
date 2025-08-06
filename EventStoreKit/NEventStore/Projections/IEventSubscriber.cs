namespace EventStoreKit.NEventStore.Projections
{
    public interface IEventSubscriber
    {
        /// <summary>
        /// Handle message
        /// </summary>
        void Handle<TEvent>( TEvent message ) where TEvent : class;

        /// <summary>
        /// Message types, which can be handled by subscriber
        /// </summary>
        IEnumerable<Type> HandledEventTypes { get; }
    }
}