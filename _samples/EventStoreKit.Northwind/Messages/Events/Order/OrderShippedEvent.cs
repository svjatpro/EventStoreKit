using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    public class OrderShippedEvent : DomainEvent
    {
        public DateTime ShippedDate { get; set; }
    }
}
