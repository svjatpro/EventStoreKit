
namespace EventStoreKit.Logging
{
    public interface ILoggerFactory
    {
        ILogger Create();
        ILogger<T> Create<T>();
    }
}