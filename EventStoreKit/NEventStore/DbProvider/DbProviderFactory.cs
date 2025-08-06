using LinqToDB.DataProvider.PostgreSQL;

namespace EventStoreKit.NEventStore.DbProvider;

public class DbProviderFactory(string connectionString) : IDbProviderFactory
{
    public IDbProvider CreateDbProvider()
    {
        return new EventStoreKit.NEventStore.DbProvider.DbProvider(PostgreSQLTools.CreateDataConnection(connectionString));
    }
}
