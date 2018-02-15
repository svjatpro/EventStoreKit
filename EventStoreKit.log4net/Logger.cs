using System;
using System.Collections.Generic;
using System.Linq;
using EventStoreKit.Logging;
using EventStoreKit.Utility;
using log4net;

namespace EventStoreKit.log4net
{
    public class Logger<T> : Logger, ILogger<T>
    {
        public Logger( ILog internalLog ) : base( internalLog )
        {
        }
    }
    public class Logger : ILogger
    {
        #region private fields

        private readonly ILog InternalLog;

        #endregion

        #region Private methods

        private void ProcessAttributes( Dictionary<string, string> attributes )
        {
            if ( attributes != null )
            {
                attributes
                  .ToList()
                  .ForEach( attr => LogicalThreadContext.Properties[attr.Key] = attr.Value );
            }
        }

        #endregion

        public Logger( ILog internalLog )
        {
            InternalLog = internalLog;
        } 
        
        public void Verbose( string message, params object[] values )
        {
            InternalLog.DebugFormat( message, values );
        }

        public void Debug( string message, params object[] values )
        {
            InternalLog.DebugFormat( message, values );
        }
        public void Debug( string message, Exception exception = null, Dictionary<string, string> attributes = null )
        {
            attributes.Do( ProcessAttributes );
            InternalLog.Debug( message, exception );
        }

        public void Info( string message, params object[] values )
        {
            InternalLog.InfoFormat( message, values );
        }
        public void Info( string message, Exception exception = null, Dictionary<string, string> attributes = null )
        {
            attributes.Do( ProcessAttributes );
            InternalLog.Info( message, exception );
        }

        public void Warn( string message, params object[] values )
        {
            InternalLog.WarnFormat( message, values );
        }
        public void Warn( string message, Exception exception = null, Dictionary<string, string> attributes = null )
        {
            attributes.Do( ProcessAttributes );
            InternalLog.Warn( message, exception );
        }

        public void Error( string message, params object[] values )
        {
            InternalLog.ErrorFormat( message, values );
        }
        public void Error( string message, Exception exception = null, Dictionary<string, string> attributes = null )
        {
            attributes.Do( ProcessAttributes );
            InternalLog.Error( message, exception );
        }

        public void Fatal( string message, params object[] values )
        {
            InternalLog.FatalFormat( message, values );
        }
        public void Fatal( string message, Exception exception = null, Dictionary<string, string> attributes = null )
        {
            attributes.Do( ProcessAttributes );
            InternalLog.Fatal( message, exception );
        }
    }
}
