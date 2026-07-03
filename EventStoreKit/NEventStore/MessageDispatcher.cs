namespace EventStoreKit.NEventStore;

public class MessageDispatcher : ICommandSender, IMessageDispatcher
{
    #region Private fields

    private readonly Dictionary<Type, List<Action<object>>> Routes = new();

    // Command handlers that return an output context (the generated entity id(s)), keyed by command type.
    private readonly Dictionary<Type, Func<object, object?>> OutputRoutes = new();

    // Optional per-command filters (see ICommandFilter<T>). Keyed by the *bare* command type so the same
    // filter applies whether dispatch wraps in IntegrationCommand<,> or not. Consulted by TrySend only.
    private readonly Dictionary<Type, Func<object, bool>> Filters = new();

    #endregion

    public void RegisterHandler<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        if (!Routes.TryGetValue(typeof(TMessage), out var handlers))
        {
            handlers = new List<Action<object>>();
            Routes.Add(typeof(TMessage), handlers);
        }
        var cast = DelegateAdjuster.CastArgument<object, TMessage>(handler);
        handlers.Add(msg => cast(msg));
    }
    
    public void RegisterExclusiveHandler<TMessage>(Action<TMessage> handler) where TMessage : class
    {
        if (Routes.ContainsKey(typeof(TMessage)))
        {
            throw new InvalidOperationException($"Handler for message type {typeof(TMessage).Name} is already registered.");
        }
        Routes.Add(typeof(TMessage), [msg => handler((TMessage)msg)]);
    }

    public void RegisterOutputHandler(Type commandType, Func<object, object?> handler)
    {
        if (!OutputRoutes.TryAdd( commandType, handler ))
        {
            throw new InvalidOperationException($"Output handler for message type {commandType.Name} is already registered.");
        }
    }

    public void RegisterFilter(Type commandType, Func<object, bool> filter)
    {
        if (!Filters.TryAdd( commandType, filter ))
        {
            throw new InvalidOperationException($"Filter for message type {commandType.Name} is already registered.");
        }
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
    }

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

    public void Send<TCommand, TContext>(TCommand command, TContext context) where TCommand : class
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var integrationCommand = IntegrationCommand.For( command, context );
        if (Routes.TryGetValue(integrationCommand.GetType(), out var handlers))
        {
            if (handlers.Count != 1)
                throw new InvalidOperationException("cannot send to more than one handler");
            handlers[0](integrationCommand);
        }
        else
        {
            throw new InvalidOperationException($"No handler registered for message {command.GetType().Name}");
        }
    }

    public bool TrySend<TCommand>( TCommand command ) where TCommand : class
    {
        if ( command == null )
            throw new ArgumentNullException( nameof(command) );

        var runtimeType = command.GetType();
        if ( !Routes.TryGetValue( runtimeType, out var handlers ) )
        {
            return false;
        }

        if ( Filters.TryGetValue( runtimeType, out var filter ) && !filter( command ) )
        {
            return false;
        }

        if ( handlers.Count != 1 )
            throw new InvalidOperationException( "cannot send to more than one handler" );

        handlers[0]( command );
        return true;
    }

    public bool TrySend<TCommand, TContext>(TCommand command, TContext context) where TCommand : class
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var integrationCommand = IntegrationCommand.For(command, context);
        var runtimeType = integrationCommand.GetType();
        if (!Routes.TryGetValue(runtimeType, out var handlers))
        {
            return false;
        }

        if (Filters.TryGetValue(command.GetType(), out var filter) && !filter(command))
        {
            return false;
        }

        if (handlers.Count != 1)
            throw new InvalidOperationException("cannot send to more than one handler");

        handlers[0](integrationCommand);
        return true;
    }

    public bool TrySend<TCommand, TContext>(TCommand command, out TContext context) where TCommand : class
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var integrationCommand = IntegrationCommand.For(command, default(TContext)!);
        if (!OutputRoutes.TryGetValue(integrationCommand.GetType(), out var handler))
        {
            context = default!;
            return false;
        }

        if (Filters.TryGetValue(command.GetType(), out var filter) && !filter(command))
        {
            context = default!;
            return false;
        }

        var result = handler(integrationCommand);
        context = result is TContext typed ? typed : default!;
        return true;
    }
}
