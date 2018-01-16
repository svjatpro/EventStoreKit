using System;
using System.Runtime.Serialization;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    [DataContract]
    public class CreateOrderDetailCommand : DomainCommand
    {
        [DataMember]public Guid OrderId { get; set; }
        [DataMember]public Guid ProductId { get; set; }
        [DataMember]public decimal UnitPrice { get; set; }
        [DataMember]public decimal Quantity { get; set; }
        [DataMember]public decimal Discount { get; set; }
    }
}
