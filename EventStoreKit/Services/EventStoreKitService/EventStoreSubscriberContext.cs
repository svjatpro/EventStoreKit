using System.Reactive.Concurrency;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Services
{
    public class EventStoreSubscriberContext : IEventStoreSubscriberContext
    {
        public ILogger Logger { get; set; }
        public IScheduler Scheduler { get; set; }
        public IEventStoreConfiguration Configuration { get; set; }
        public IDbProviderFactory DbProviderFactory { get; set; }
    }
}