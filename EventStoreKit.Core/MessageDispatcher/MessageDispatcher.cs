using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Threading.Tasks;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Services;
using EventStoreKit.Utility;

namespace EventStoreKit.Core
{
    public class MessageDispatcher<TBasic> : IMessageDispatcher<TBasic> where TBasic : class
    {
        #region Private fields

        private readonly ILogger<MessageDispatcher<TBasic>> Logger;
        private readonly Dictionary<Type, List<Action<TBasic>>> Routes = new Dictionary<Type, List<Action<TBasic>>>();

        #endregion

        public MessageDispatcher( ILogger<MessageDispatcher<TBasic>> logger )
        {
            Logger = logger;
        }

        public void RegisterHandler<TMessage>( Action<TMessage> handler, bool allowMultiple = true ) where TMessage : TBasic
        {
            if( !Routes.TryGetValue( typeof(TMessage), out var handlers ) )
            {
                handlers = new List<Action<TBasic>>();
                Routes.Add( typeof(TMessage), handlers );
            }
            if( !allowMultiple && handlers.Count != 0  )
                throw new InvalidOperationException( "Multiple routes is not allowed for this message");

            handlers.Add( DelegateAdjuster.CastArgument<TBasic, TMessage>( handler ) );
            Logger.Debug( "Handler registered: {0}", typeof(TMessage).Name );
        }

        public void Dispatch( object message )
        {
            message?.OfType<TBasic>().Do( Dispatch );
        }

        public void Dispatch<TMessage>( TMessage message ) where TMessage : TBasic
        {
            if( message == null )
                throw new ArgumentNullException( nameof(message) );

            if( !Routes.TryGetValue( message.GetType(), out List<Action<TBasic>> handlers ) )
                return;
            handlers
                .ToList()
                .ForEach( handler => handler( message ) );

            Logger.Debug( $"Event dispatched: {message.GetType().Name}" );
        }
    }
}
