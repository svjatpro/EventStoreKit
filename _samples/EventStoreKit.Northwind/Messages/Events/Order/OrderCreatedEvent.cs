using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class OrderCreatedEvent : DomainEvent
    {
        public Guid CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime RequiredDate { get; set; }
    }
}
