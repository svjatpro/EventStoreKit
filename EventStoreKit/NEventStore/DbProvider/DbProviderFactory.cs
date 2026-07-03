using LinqToDB.Data;
using LinqToDB.DataProvider.PostgreSQL;

namespace EventStoreKit.NEventStore.DbProvider;

public class DbProviderFactory : IDbProviderFactory
{
    private readonly string DefaultSchema = "Default";
    private readonly Dictionary<string, Func<DataConnection>> ConnectionFactories;
    private readonly Action<DataConnection>? InitializeDbProvider;

    // Schemas reachable via CreateDbProvider but NOT owned by the store — e.g. the adapters schema
    // points at the legacy DB. Excluded from Schemas so CleanUp never truncates legacy tables.
    private readonly HashSet<string> ExternalSchemas;

    public DbProviderFactory( string connectionString, Action<DataConnection>? initializeDbProvider = null )
    {
        ConnectionFactories = new Dictionary<string, Func<DataConnection>>
        {
            { DefaultSchema, () => PostgreSQLTools.CreateDataConnection( connectionString ) },
        };
        InitializeDbProvider = initializeDbProvider;
        ExternalSchemas = new HashSet<string>();
    }

    public DbProviderFactory(
        Dictionary<string, string> connectionStrings,
        string? defaultSchema,
        Action<DataConnection>? initializeDbProvider = null )
        : this(
            connectionStrings.ToDictionary(
                kv => kv.Key,
                kv => (Func<DataConnection>)( () => PostgreSQLTools.CreateDataConnection( kv.Value ) ) ),
            defaultSchema,
            initializeDbProvider )
    {
    }

    // Per-schema connection factories — lets one schema (e.g. adapters) encapsulate a custom
    // connection (carrying the legacy EF mapping schema) while the rest stay plain connection-string.
    public DbProviderFactory(
        Dictionary<string, Func<DataConnection>> connectionFactories,
        string? defaultSchema,
        Action<DataConnection>? initializeDbProvider = null,
        IEnumerable<string>? externalSchemas = null )
    {
        ConnectionFactories = connectionFactories;
        if ( defaultSchema != null && ConnectionFactories.ContainsKey( defaultSchema ) )
        {
            DefaultSchema = defaultSchema;
        }

        InitializeDbProvider = initializeDbProvider;
        ExternalSchemas = externalSchemas?.ToHashSet() ?? new HashSet<string>();
    }

    // Owned schemas only (external/adapter schemas excluded) — this is what CleanUp truncates.
    public IReadOnlyCollection<string> Schemas => ConnectionFactories.Keys
        .Where( s => !ExternalSchemas.Contains( s ) )
        .ToList();

    public IDbProvider CreateDbProvider()
    {
        return CreateDbProvider( DefaultSchema );
    }

    public IDbProvider CreateDbProvider( string schema )
    {
        if ( !ConnectionFactories.TryGetValue( schema, out var connectionFactory ) )
        {
            throw new ArgumentException($"Schema '{schema}' is not configured.");
        }
        var dbConnection = connectionFactory();
        InitializeDbProvider?.Invoke( dbConnection );
        return new DbProvider( dbConnection );
    }
}
