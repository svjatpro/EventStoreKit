using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class CustomerRenamedEvent : DomainEvent
    {
        public string CompanyName { get; set; }
    }
}
