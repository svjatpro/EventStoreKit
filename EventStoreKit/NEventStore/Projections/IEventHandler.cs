namespace EventStoreKit.NEventStore.Projections
{
    public interface IEventHandler<in TEvent>
        where TEvent : class
    {
        void Handle( TEvent message );
    }
}