using System;
using System.Runtime.Serialization;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    [DataContract]
    public class RemoveOrderDetailCommand : DomainCommand
    {
        [DataMember]public Guid OrderId { get; set; }
    }
}
