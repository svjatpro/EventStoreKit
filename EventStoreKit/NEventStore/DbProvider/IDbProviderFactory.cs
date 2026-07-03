namespace EventStoreKit.NEventStore.DbProvider;

public interface IDbProviderFactory
{
    IDbProvider CreateDbProvider();
    IDbProvider CreateDbProvider( string schema );

    IReadOnlyCollection<string> Schemas { get; }
}
