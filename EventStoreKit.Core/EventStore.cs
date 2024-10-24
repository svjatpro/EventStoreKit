// ReSharper disable TypeParameterCanBeVariant
namespace EventStoreKit.Core
{
    public interface ICommandHandler<TCommand, TAggregate> 
        where TCommand : class
        where TAggregate : class
    {
        TEvent Handle( TCommand command );
    }

    public interface ICommandHandler<TCommand> where TCommand : class
    {
        IEnumerable<object> Handle( TCommand command );
    }

    public interface IEventStore
    {
    }

    public class EventStore : IEventStore
    {

    }
}
