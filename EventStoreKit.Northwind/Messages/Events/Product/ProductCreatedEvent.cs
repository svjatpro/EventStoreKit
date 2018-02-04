using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class ProductCreatedEvent : DomainEvent
    {
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
