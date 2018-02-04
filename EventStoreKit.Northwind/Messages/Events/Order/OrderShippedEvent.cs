using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class OrderShippedEvent : DomainEvent
    {
        public DateTime ShippedDate { get; set; }
    }
}
