using System;

namespace EventStoreKit.Messages
{
    public class SequenceMarkerEvent : Message
    {
        public Guid Identity { get; set; }
    }
}
