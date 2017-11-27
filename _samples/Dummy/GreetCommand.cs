using EventStoreKit.Messages;

namespace Dummy
{
    public class GreetCommand : DomainCommand
    {
        public string Object { get; set; }
    }
}