
using System;
using System.Collections.Generic;

namespace EventStoreKit.Projections
{
    public interface IReadModelOwner : IEventSubscriber
    {
        Type GetPrimaryReadModel { get; }
        List<Type> GetReadModels { get; }
    }
}
