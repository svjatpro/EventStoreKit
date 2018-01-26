using EventStoreKit.Services;

namespace EventStoreKit.DbProviders
{
    public interface IDbProviderFactory
    {
        /// <summary>
        /// Create DbProvider instance
        /// </summary>
        IDbProvider Create();

        /// <summary>
        /// Create DbProvider instance by configuration
        /// </summary>
        IDbProvider Create( IDataBaseConfiguration configuration );
    }
}