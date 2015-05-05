using System;

namespace EventStoreKit.Messages
{
    //[DataContract]
    public abstract class Message
    {
        //[DataMember]
        public int Version { get; set; }

        //[DataMember]
        public DateTime Created { get; set; }

        //[DataMember]
        public Guid CreatedBy { get; set; }
    }
}
