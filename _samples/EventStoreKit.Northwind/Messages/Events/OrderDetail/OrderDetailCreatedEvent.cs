using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class OrderDetailCreatedEvent : DomainCommand
    {
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Discount { get; set; }
    }
}
