using System;
using EventStoreKit.Messages;

namespace EventStoreKit.Core.Sagas
{
    public interface ISagaIdGenerator
    {
        Type SagaType { get; }
        string GetSagaId( Message message );
    }
}