namespace EventStoreKit.DbProviders
{
    public interface IDbProviderFactory
    {
        /// <summary>
        /// Create DbProvider instance
        /// </summary>
        IDbProvider Create();

        /// <summary>
        /// Create DbProvider instance
        /// </summary>
        IDbProvider Create<TModel>() where TModel : class;
    }
}