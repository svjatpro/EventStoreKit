namespace EventStoreKit.NEventStore;

public class MessageDispatcher : ICommandSender, IMessageDispatcher
{
    #region Private fields

    private readonly Dictionary<Type, List<Action<object>>> Routes = new();

    #endregion

    public void RegisterHandler<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        if (!Routes.TryGetValue(typeof(TMessage), out var handlers))
        {
            handlers = new List<Action<object>>();
            Routes.Add(typeof(TMessage), handlers);
        }
        handlers.Add(DelegateAdjuster.CastArgument<object, TMessage>(handler));
    }

    public void RegisterExclusiveHandler<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        if (Routes.TryGetValue(typeof(TMessage), out var handlers))
        {
            throw new InvalidOperationException($"Handler for message type {typeof(TMessage).Name} is already registered.");
        }
        Routes.Add(typeof(TMessage), [DelegateAdjuster.CastArgument<object, TMessage>(handler)]);
    }

    public void Dispatch<TMessage>(TMessage? message) where TMessage : class
    {
        if (message == null) return;
        if (Routes.TryGetValue(message.GetType(), out var handlers))
        {
            foreach (var handler in handlers)
            {
                handler(message);
            }
        }
        //else
        //{
        //    throw new InvalidOperationException($"No handler registered for message {message.GetType().Name}");
        //}
    }

    //public void Publish<TEvent>( TEvent @event ) where TEvent : Message
    //{
    //    Publish( (Message)@event );
    //}

    //public void Publish( Message @event )
    //{
    //    if ( @event == null )
    //        throw new ArgumentNullException( "event" );

    //    List<Action<Message>> handlers;
    //    if ( !Routes.TryGetValue( @event.GetType(), out handlers ) )
    //        return;
    //    foreach ( Action<Message> handler in handlers )
    //    {
    //        var handler1 = handler;
    //        handler1( @event );
    //    }
    //}

    public void Send<TCommand>( TCommand command ) where TCommand : class
    {
        if ( command == null )
            throw new ArgumentNullException( nameof(command) );

        if ( Routes.TryGetValue( command.GetType(), out var handlers ) )
        {
            if ( handlers.Count != 1 )
                throw new InvalidOperationException( "cannot send to more than one handler" );
            handlers[0]( command );
        }
        else
        {
            throw new InvalidOperationException($"No handler registered for message {command.GetType().Name}");
        }
    }
}