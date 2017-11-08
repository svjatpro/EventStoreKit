namespace EventStoreKit.DbProviders
{
    public interface IDbProviderFactory
    {
        /// <summary>
        /// Create DbProvider instance for event store Db
        /// </summary>
        IDbProvider CreateEventStoreProvider();

        /// <summary>
        /// Create DbProvider instance for projectiona Db
        /// </summary>
        IDbProvider CreateProjectionProvider();
    }
}