using System;
using log4net;

namespace EventStoreKit.Logging
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
