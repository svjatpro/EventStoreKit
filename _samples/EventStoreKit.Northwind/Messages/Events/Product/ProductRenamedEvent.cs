using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class ProductRenamedEvent : DomainEvent
    {
        public string ProductName { get; set; }
    }
}
