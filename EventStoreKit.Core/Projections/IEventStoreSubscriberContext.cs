using System.Reactive.Concurrency;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Services
{
    public interface IEventStoreSubscriberContext
    {
        IEventStoreConfiguration Configuration { get; }
        ILoggerFactory LoggerFactory { get; }
        IScheduler Scheduler { get; }
        IDbProviderFactory DbProviderFactory { get; }
    }
}