using System;
using System.Collections.Generic;
using System.Linq;
using EventStoreKit.Messages;
using EventStoreKit.Utility;

namespace EventStoreKit.Projections.MessageHandler
{
    internal class DirectMessageHandler<TMessage> : IMessageHandler 
        where TMessage : Message
    {
        #region Private fields

        protected readonly List<Action<Message>> Actions;
        
        #endregion

        public DirectMessageHandler( Action<Message> action )
        {
            Actions = new List<Action<Message>> {action};
        }
        public DirectMessageHandler( IEnumerable<Action<Message>> actions )
        {
            Actions = actions.ToList();
        }
        public DirectMessageHandler( params Action<Message>[] actions )
        {
            Actions = actions.ToList();
        }

        public Type Type { get { return typeof(TMessage); } }
        public bool IsAlive { get { return true; } }

        public void Process( Message message )
        {
            message
                .OfType<TMessage>()
                .Do( msg => Actions.ForEach( action => action( msg ) ) );
        }

        public IMessageHandler Combine( Action<Message> process, bool runBefore = false )
        {
            var actions = Actions.ToList();
            if ( runBefore )
            {
                actions.Insert( 0, process );
            }
            else
            {
                actions.Add( process );
            }
            return new DirectMessageHandler<TMessage>( actions );
        }
    }
}