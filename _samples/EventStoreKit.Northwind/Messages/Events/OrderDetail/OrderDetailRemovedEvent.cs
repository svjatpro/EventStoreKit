using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class OrderDetailRemovedEvent : DomainEvent
    {
        public Guid OrderId { get; set; }
    }
}
