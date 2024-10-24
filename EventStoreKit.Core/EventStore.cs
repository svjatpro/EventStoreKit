// ReSharper disable TypeParameterCanBeVariant
namespace EventStoreKit.Core
{
    public interface ICommandHandler<TCommand, TAggregate> 
        where TCommand : class
        where TAggregate : class
    {
        IEnumerable<object> Handle( TCommand command, TAggregate aggregate );
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
