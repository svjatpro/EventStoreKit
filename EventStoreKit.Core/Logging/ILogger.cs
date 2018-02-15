using System;
using System.Collections.Generic;
using NEventStore.Logging;

namespace EventStoreKit.Logging
{
    public interface ILogger : ILog
    {
        void Debug( string message, Exception exception = null, Dictionary<string, string> attributes = null );
        void Info( string message, Exception exception = null, Dictionary<string, string> attributes = null );
        void Warn( string message, Exception exception = null, Dictionary<string, string> attributes = null );
        void Error( string message, Exception exception = null, Dictionary<string, string> attributes = null );
        void Fatal( string message, Exception exception = null, Dictionary<string, string> attributes = null );
    }

    public interface ILogger<T> : ILogger
    {
    }
}