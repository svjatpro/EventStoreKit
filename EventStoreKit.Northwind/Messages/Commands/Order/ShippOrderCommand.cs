using System;
using System.Runtime.Serialization;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    [DataContract]
    public class ShippOrderCommand : DomainCommand
    {
        [DataMember]public DateTime ShippedDate { get; set; }
    }
}
