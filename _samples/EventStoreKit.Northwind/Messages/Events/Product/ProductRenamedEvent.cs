using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    public class ProductRenamedEvent : DomainCommand
    {
        public string ProductName { get; set; }
    }
}
