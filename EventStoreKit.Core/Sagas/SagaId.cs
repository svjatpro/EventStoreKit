using System;
using CommonDomain;
using EventStoreKit.Messages;

namespace EventStoreKit.Core.Sagas
{
    public static class SagaId
    {
        public static ISagaIdGeneratorMapping For<TSaga>() where TSaga : ISaga
        {
            return new SagaIdGenerator<TSaga>();
        }

        public static ISagaIdGeneratorMapping From<TMessage>( this ISagaIdGeneratorMapping mapping, Func<TMessage, string> idGetter ) where TMessage : Message
        {
            mapping.MappMessage( idGetter );
            return mapping;
        }
    }
}