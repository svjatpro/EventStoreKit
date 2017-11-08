using System.Runtime.Serialization;
using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Commands
{
    [DataContract]
    public class UpdateCustomerCommand : DomainCommand
    {
        [DataMember]public string CompanyName { get; set; }
        
        [DataMember]public string ContactName { get; set; }
        [DataMember]public string ContactTitle { get; set; }
        [DataMember]public string ContactPhone { get; set; }

        [DataMember]public string Address { get; set; }
        [DataMember]public string City { get; set; }
        [DataMember]public string Region { get; set; }
        [DataMember]public string Country { get; set; }
        [DataMember]public string PostalCode { get; set; }
    }
}