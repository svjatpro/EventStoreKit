using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class OrderDetailRemovedEvent : DomainCommand
    {
        public Guid OrderId { get; set; }
    }
}
