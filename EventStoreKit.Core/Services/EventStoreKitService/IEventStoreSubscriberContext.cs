using System.Reactive.Concurrency;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Services
{
    public interface IEventStoreSubscriberContext
    {
        ILogger Logger { get; }
        IScheduler Scheduler { get; }
        IEventStoreConfiguration Configuration { get; }
        IDbProviderFactory DbProviderFactory { get; }
    }
}