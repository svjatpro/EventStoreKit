using System;

namespace EventStoreKit.Services.IdGenerators
{
    public interface IIdGenerator
    {
        Guid NewGuid();
    }
}
