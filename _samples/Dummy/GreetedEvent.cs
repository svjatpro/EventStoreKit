using EventStoreKit.Messages;

namespace Dummy
{
    public class GreetedEvent : DomainEvent
    {
        public string HelloMessage { get; set; }
    }
}