using System.Reactive.Concurrency;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Services
{
    public class EventStoreSubscriberContext : IEventStoreSubscriberContext
    {
        public IEventStoreConfiguration Configuration { get; set; }
        public ILoggerFactory LoggerFactory { get; set; }
        public IScheduler Scheduler { get; set; }
        public IDbProviderFactory DbProviderFactory { get; set; }

        public EventStoreSubscriberContext( 
            IEventStoreConfiguration configuration, 
            ILoggerFactory loggerFactory,
            IScheduler scheduler, 
            IDbProviderFactory dbProviderFactory )
        {
            Configuration = configuration;
            LoggerFactory = loggerFactory;
            Scheduler = scheduler;
            DbProviderFactory = dbProviderFactory;
        }
    }
}