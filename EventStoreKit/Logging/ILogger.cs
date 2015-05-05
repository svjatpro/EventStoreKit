using log4net;
using log4net.Core;

namespace EventStoreKit.Logging
{
    public interface ILogger<T> : ILog { }
    public class Logger<T> : LogImpl, ILogger<T>
    {
        public Logger() : base( LogManager.GetLogger( typeof( T ) ).Logger ){}
    }
}