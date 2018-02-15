using System;
using System.Collections.Generic;
using EventStoreKit.Logging;
using EventStoreKit.Messages;

namespace EventStoreKit.Services
{
    public class MessageDispatcher : IEventPublisher, IEventDispatcher, ICommandBus
    {
        #region Private fields

        private readonly ILogger<MessageDispatcher> Logger;
        private readonly Dictionary<Type, List<Action<Message>>> Routes = new Dictionary<Type, List<Action<Message>>>();

        #endregion

        public MessageDispatcher( ILogger<MessageDispatcher> logger )
        {
            Logger = logger;
        }

        public void RegisterHandler<T>( Action<T> handler ) where T : Message
        {
            List<Action<Message>> handlers;
            if ( !Routes.TryGetValue( typeof (T), out handlers ) )
            {
                handlers = new List<Action<Message>>();
                Routes.Add( typeof (T), handlers );
            }
            handlers.Add( DelegateAdjuster.CastArgument<Message, T>( handler ) );
            Logger.Debug( "Handler registered: {0}", typeof (T).Name );
        }
        
        public void Publish<TEvent>( TEvent @event ) where TEvent : Message
        {
            Publish( (Message)@event );
        }

        public void Publish( Message @event )
        {
            if ( @event == null )
                throw new ArgumentNullException( "event" );

            List<Action<Message>> handlers;
            if ( !Routes.TryGetValue( @event.GetType(), out handlers ) )
                return;
            foreach ( Action<Message> handler in handlers )
            {
                var handler1 = handler;
                handler1( @event );
            }
            Logger.Debug( "Event published: {0}", @event.GetType().Name );
        }
        
        public void Send<TCommand>( TCommand command ) where TCommand : DomainCommand
        {
            if ( command == null )
                throw new ArgumentNullException( "command" );

            List<Action<Message>> handlers;
            if ( Routes.TryGetValue( command.GetType(), out handlers ) )
            {
                if ( handlers.Count != 1 )
                    throw new InvalidOperationException( "cannot send to more than one handler" ); // todo: check on registration
                handlers[0]( command );
            }
            else
            {
                throw new InvalidOperationException( string.Format( "No handler registered for message {0}", command.GetType().Name ) );
            }
        }
    }
}
