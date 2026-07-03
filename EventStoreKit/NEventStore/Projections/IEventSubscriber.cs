namespace EventStoreKit.NEventStore.Projections
{
    public interface IEventSubscriber
    {
        /// <summary>
        /// Handle message
        /// </summary>
        void HandleEvent( object message );

        /// <summary>
        /// Message types, which can be handled by subscriber
        /// </summary>
        IEnumerable<Type> HandledEventTypes { get; }
    }
}