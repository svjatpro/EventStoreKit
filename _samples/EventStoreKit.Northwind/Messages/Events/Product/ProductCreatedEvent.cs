using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    public class ProductCreatedEvent : DomainCommand
    {
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
