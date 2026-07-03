using EventStoreKit.NEventStore;
using NUnit.Framework;

namespace EventStoreKit.Tests.Shared;

// Base fixture: a fresh in-memory NEventStoreKit per test (InstancePerTestCase, parallel-safe).
public abstract class InMemoryStoreFixture
{
    protected NEventStoreKit Service = null!;

    [SetUp]
    public void Setup()
    {
        Service = new NEventStoreKit(new NoSagas());
        Service.InitializeInMemory();
    }

    [TearDown]
    public void Teardown() => Service?.Dispose();
}
