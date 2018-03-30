using System;
using System.Runtime.Serialization;

namespace EventStoreKit.Messages
{
    public abstract class Message
    {
        public Guid Id { get; set; }

        public int Version { get; set; }

        public string BucketId { get; set; }

        public DateTime Created { get; set; }

        public Guid CreatedBy { get; set; }



        /// <summary>
        /// Indicates, that the message is a part of message group, logically related
        /// </summary>
        [IgnoreDataMember]
        public bool IsBulk { get; set; }
    }
}
