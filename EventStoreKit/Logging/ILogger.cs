using System;
using System.Collections.Generic;
using Microsoft.SqlServer.Server;

namespace EventStoreKit.Logging
{
    //public interface ILogger<T> : ILog { }
    //public class Logger<T> : LogImpl, ILogger<T>
    //{
    //    public Logger() : base( LogManager.GetLogger( typeof( T ) ).Logger ){}
    //}


    public interface ILogger
    {
        //void SetAttribute( string name, string value );

        void Debug( string message );
        void Debug( string message, Exception exception );
        void DebugFormat( string message, params object[] args );

        void Info( string message );
        void Info( string message, Exception exception );
        void InfoFormat( string message, params object[] args );

        void Warn( string message );
        void Warn( string message, Exception exception );
        void WarnFormat( string message, params object[] args );

        void Error( string message );
        void Error( string message, Exception exception, Dictionary<string,string> attributes = null );
        void ErrorFormat( string message, params object[] args );

        void Fatal( string message );
        void Fatal( string message, Exception exception );
        void FatalFormat( string message, params object[] args );
    }

    public interface ILogger<T> : ILogger
    {
        
    }
}