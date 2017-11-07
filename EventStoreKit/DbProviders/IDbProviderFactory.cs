namespace EventStoreKit.DbProviders
{
    public interface IDbProviderFactory
    {
        IDbProvider CreateByConnectionString( SqlClientType clientType, string connectionString );
        IDbProvider CreateByConfiguration( string configurationString );
    }
}