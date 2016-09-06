using NEventStore.Logging;

namespace EventStoreKit.Logging
{
    public class StoreLoggerAdater<T> : ILog
    {
        private readonly ILogger<T> Log;

        public StoreLoggerAdater( ILogger<T> log )
        {
            Log = log;
            //log4net.LogManager.GetLogger( typeToLog );
        }

        public virtual void Verbose( string message, params object[] values )
        {
            Log.DebugFormat( message, values );
        }

        public virtual void Debug( string message, params object[] values )
        {
            Log.DebugFormat( message, values );
        }

        public virtual void Info( string message, params object[] values )
        {
            Log.InfoFormat( message, values );
        }

        public virtual void Warn( string message, params object[] values )
        {
            Log.WarnFormat( message, values );
        }

        public virtual void Error( string message, params object[] values )
        {
            Log.ErrorFormat( message, values );
        }

        public virtual void Fatal( string message, params object[] values )
        {
            Log.FatalFormat( message, values );
        }
    }
}