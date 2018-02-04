using System;
using System.Collections.Generic;

namespace EventStoreKit.Logging
{
    public class LoggerStub<T> : ILogger<T>
    {
        public void Verbose(string message, params object[] values){}
        public void Debug(string message, params object[] values){}
        public void Info(string message, params object[] values){}
        public void Warn(string message, params object[] values){}
        public void Error(string message, params object[] values) { Console.WriteLine( message ); }
        public void Fatal(string message, params object[] values) { Console.WriteLine(message); }
        public void Debug(string message, Exception exception = null, Dictionary<string, string> attributes = null) {}
        public void Info(string message, Exception exception = null, Dictionary<string, string> attributes = null) {}
        public void Warn(string message, Exception exception = null, Dictionary<string, string> attributes = null) {}
        public void Error(string message, Exception exception = null, Dictionary<string, string> attributes = null) { Console.WriteLine(exception); }
        public void Fatal(string message, Exception exception = null, Dictionary<string, string> attributes = null) { Console.WriteLine(exception); }
    }
}