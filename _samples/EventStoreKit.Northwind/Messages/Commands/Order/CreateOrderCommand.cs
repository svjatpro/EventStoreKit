using System;
using System.Runtime.Serialization;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    [DataContract]
    public class CreateOrderCommand : DomainCommand
    {
        [DataMember]public string CustomerId { get; set; }
        [DataMember]public DateTime OrderDate { get; set; }
        [DataMember]public DateTime RequiredDate { get; set; }
    }
}
