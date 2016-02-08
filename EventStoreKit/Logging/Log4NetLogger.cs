using System;
using NEventStore.Logging;

namespace EventStoreKit.Logging
{
    public class Log4NetLogger : ILog
    {
        private readonly log4net.ILog Log;

        public Log4NetLogger( Type typeToLog )
        {
            Log = log4net.LogManager.GetLogger( typeToLog );
        }

        public virtual void Verbose( string message, params object[] values )
        {
            if ( Log.IsDebugEnabled )
                Log.DebugFormat( message, values );
        }

        public virtual void Debug( string message, params object[] values )
        {
            if ( Log.IsDebugEnabled )
                Log.DebugFormat( message, values );
        }

        public virtual void Info( string message, params object[] values )
        {
            if ( Log.IsInfoEnabled )
                Log.InfoFormat( message, values );
        }

        public virtual void Warn( string message, params object[] values )
        {
            if ( Log.IsWarnEnabled )
                Log.WarnFormat( message, values );
        }

        public virtual void Error( string message, params object[] values )
        {
            if ( Log.IsErrorEnabled )
                Log.ErrorFormat( message, values );
        }

        public virtual void Fatal( string message, params object[] values )
        {
            if ( Log.IsFatalEnabled )
                Log.FatalFormat( message, values );
        }
    }
}