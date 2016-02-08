using System;

namespace EventStoreKit.Messages
{
    public class CommandDispatchFailedEvent : DomainEvent
    {
        public CommandDispatchFailedEvent( Guid correlationId )
        {
            Id = correlationId;
        }
    }
}