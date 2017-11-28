using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    public class ProductPriceUpdatedEvent : DomainCommand
    {
        public decimal UnitPrice { get; set; }
    }
}
