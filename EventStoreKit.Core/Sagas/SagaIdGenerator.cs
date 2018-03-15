using System;
using System.Collections.Generic;
using CommonDomain;
using EventStoreKit.Messages;
using EventStoreKit.Utility;

namespace EventStoreKit.Core.Sagas
{
    /// <summary>
    /// SagaId generation rules, mapped to message types 
    /// </summary>
    public class SagaIdGenerator<TSaga> : ISagaIdGeneratorMapping
        where TSaga : ISaga
    {
        private Dictionary<Type, Func<Message, string>> Mapping;

        public string GetSagaId( Message message )
        {
            var sagaType = typeof(TSaga);
            var messageType = message.GetType();
            var id =
                Mapping.With( idMap => idMap.ContainsKey( messageType ) ) ?
                    Mapping[messageType]( message ) :
                    $"{sagaType.Name}_{message.Id}";
            return id;
        }

        public Type SagaType { get; } = typeof(TSaga);

        public void MappMessage<TMessage>( Func<TMessage, string> idGetter ) where TMessage : Message
        {
            if ( Mapping == null )
                Mapping = new Dictionary<Type, Func<Message, string>>();
            Mapping.Add( typeof(TMessage), msg => msg.OfType<TMessage>().With( idGetter ) );
        }
    }
}