using System;

namespace EventStoreKit.Services
{
    public interface ICurrentUserProvider
    {
        Guid CurrentUserId { get; }
    }
}