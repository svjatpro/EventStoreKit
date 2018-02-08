using EventStoreKit.Services;

namespace EventStoreKit.Logging
{
    public class LoggerFactoryStub : ILoggerFactory
    {
        public ILogger Create()
        {
            return new LoggerStub<EventStoreKitService>();
        }

        public ILogger<T> Create<T>()
        {
            return new LoggerStub<T>();
        }
    }
}