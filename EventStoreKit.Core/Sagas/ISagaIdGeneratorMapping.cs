using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Core.Sagas
{
    public interface ISagaIdGeneratorMapping : ISagaIdGenerator
    {
        void MappMessage<TMessage>( Func<TMessage, string> idGetter ) where TMessage : Message;
    }
}