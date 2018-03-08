using System;
using System.Collections.Generic;
using EventStoreKit.Messages;
using EventStoreKit.Utility;

namespace EventStoreKit.Services
{
    public static class SagaId
    {
        public static Dictionary<Type, Func<Message, string>> From<TMessage>( Func<TMessage,string> idGetter ) where TMessage : Message
        {
            return new Dictionary<Type, Func<Message, string>>{ { typeof(TMessage), msg => msg.OfType<TMessage>().With( idGetter ) }};
        }

        public static Dictionary<Type, Func<Message, string>> From<TMessage>( this Dictionary<Type, Func<Message, string>> map, Func<TMessage, string> idGetter )
            where TMessage : Message
        {
            map.Add( typeof(TMessage), msg => msg.OfType<TMessage>().With( idGetter ) );
            return map;
        }

        public static string GetSagaId( this Dictionary<Type, Func<Message, string>> map, Type sagaType, Message message )
        {
            var messageType = message.GetType();
            var id =
                map.With( idMap => idMap.ContainsKey( messageType ) ) ?
                map[messageType]( message ) :
                $"{sagaType.Name}_{message.Id}";
            return id;
        }
    }
}