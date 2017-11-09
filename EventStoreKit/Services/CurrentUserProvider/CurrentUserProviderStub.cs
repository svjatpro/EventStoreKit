using System;

namespace EventStoreKit.Services
{
    public class CurrentUserProviderStub : ICurrentUserProvider
    {
        public Guid? CurrentUserId { get; set; }
    }
}