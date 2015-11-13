using System;
using System.Runtime.Serialization;

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

        /// <summary>
        /// Indicates, that the message is a part of message group, logically related
        /// </summary>
        [IgnoreDataMember]
        public bool IsBulk { get; set; }
    }
}
