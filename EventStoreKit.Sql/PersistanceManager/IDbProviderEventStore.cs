
namespace EventStoreKit.Sql.PersistanceManager
{
    public interface IDbProviderEventStore : IDbProvider{}

    public class DbProviderEventStore : DbProvider, IDbProviderEventStore
    {
        public DbProviderEventStore( string connectionStringName ) : base( connectionStringName ) { }
    }
}
