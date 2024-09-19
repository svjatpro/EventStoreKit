using EventStoreKit.Core.EventStore;

namespace EventStoreKit.NEventStore
{
    public class EventStoreAdapter : IEventStore
    {
        public EventStoreAdapter()
        {
            
        }

        public void AppendToStream( string streamId, params IMessage[] messages )
        {
            
        }
    }
}
