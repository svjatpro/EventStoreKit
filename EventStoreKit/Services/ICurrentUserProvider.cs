using System;

namespace EventStoreKit.Services
{
    public interface ICurrentUserProvider
    {
        Guid? CurrentUserId { get; }
    }

    public class CurrentUserProviderStub : ICurrentUserProvider
    {
        public Guid? CurrentUserId { get; set; }
    }
}