using System.Reactive.Concurrency;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Services
{
    public class EventStoreSubscriberContext : IEventStoreSubscriberContext
    {
        public IEventStoreConfiguration Configuration { get; set; }
        public ILogger Logger { get; set; }
        public IScheduler Scheduler { get; set; }
        public IDbProviderFactory DbProviderFactory { get; set; }

        public EventStoreSubscriberContext( 
            IEventStoreConfiguration configuration, 
            ILogger logger, 
            IScheduler scheduler, 
            IDbProviderFactory dbProviderFactory )
        {
            Configuration = configuration;
            Logger = logger;
            Scheduler = scheduler;
            DbProviderFactory = dbProviderFactory;
        }
    }
}