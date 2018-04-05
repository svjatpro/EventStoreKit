using System;

namespace EventStoreKit.Messages
{
    public abstract class Message
    {



        public Guid Id { get; set; }

        public int Version { get; set; }

        public string BucketId { get; set; }

        public DateTime Created { get; set; }

        public Guid CreatedBy { get; set; }
    }
}