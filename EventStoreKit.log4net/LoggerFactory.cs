using System;
using EventStoreKit.Logging;
using log4net;

namespace EventStoreKit.log4net
{
    public class LoggerFactory : ILoggerFactory
    {
        public ILogger Create()
        {
            return new Logger( LogManager.GetLogger( "EventStoreKit" ) );
        }

        public ILogger<T> Create<T>()
        {
            return new Logger<T>( LogManager.GetLogger( typeof( T ) ) );
        }

        public ILogger Create( Type type )
        {
            return new Logger( LogManager.GetLogger( type ) );
        }
    }
}
