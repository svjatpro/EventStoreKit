using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class CustomerContactChangedEvent : DomainEvent
    {
        public string ContactName { get; set; }
        public string ContactTitle { get; set; }
        public string ContactPhone { get; set; }
    }
}
