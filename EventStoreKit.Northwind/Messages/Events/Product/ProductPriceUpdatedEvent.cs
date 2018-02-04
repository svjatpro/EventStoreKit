using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class ProductPriceUpdatedEvent : DomainEvent
    {
        public decimal UnitPrice { get; set; }
    }
}
