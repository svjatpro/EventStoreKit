namespace EventStoreKit.DbProviders
{
    public interface IDbProviderFactory
    {
        /// <summary>
        /// Create DbProvider instance
        /// </summary>
        IDbProvider Create();
    }
}