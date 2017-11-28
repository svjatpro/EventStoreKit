using System.Runtime.Serialization;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    [DataContract]
    public class UpdateProductCommand : DomainCommand
    {
        [DataMember]public string ProductName { get; set; }
        [DataMember]public decimal UnitPrice { get; set; }
    }
}
