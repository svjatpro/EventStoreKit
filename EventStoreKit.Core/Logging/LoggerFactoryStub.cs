using System;

namespace EventStoreKit.Logging
{
    public class LoggerFactoryStub : ILoggerFactory
    {
        public ILogger Create()
        {
            return new LoggerStub();
        }

        public ILogger<T> Create<T>()
        {
            return new LoggerStub<T>();
        }

        public ILogger Create( Type type )
        {
            return new LoggerStub();
        }
    }
}