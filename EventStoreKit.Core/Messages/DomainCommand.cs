using System;
using System.Runtime.Serialization;

namespace EventStoreKit.Messages
{
    [DataContract]
    public abstract class DomainCommand : Message
    {
        //[DataMember]
        //public Guid Id { get; set; }
    }
}