using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    public class OrderCreatedEvent : DomainCommand
    {
        public string CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime RequiredDate { get; set; }
    }
}
