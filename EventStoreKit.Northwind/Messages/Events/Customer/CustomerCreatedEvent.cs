using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class CustomerCreatedEvent : DomainEvent
    {
        public string CompanyName { get; set; }

        public string ContactName { get; set; }
        public string ContactTitle { get; set; }
        public string ContactPhone { get; set; }

        public string Address { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
    }
}
