namespace EventStoreKit.NEventStore.DbProvider;

public interface IDbProviderFactory
{
    IDbProvider CreateDbProvider();
}
