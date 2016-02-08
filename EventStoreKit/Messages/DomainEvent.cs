using System;

namespace EventStoreKit.Messages
{
    public abstract class DomainEvent : Message
    {
        public Guid Id { get; set; }
    }
}
