using System;
using EventStoreKit.Services;

namespace EventStoreKit.Northwind.Console
{
    public class CurrentUserProviderStub : ICurrentUserProvider
    {
        private Guid Id = Guid.NewGuid();
        public Guid? CurrentUserId { get { return Id; } }
    }
}
