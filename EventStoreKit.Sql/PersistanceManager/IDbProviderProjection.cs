
namespace EventStoreKit.Sql.PersistanceManager
{
    public interface IDbProviderProjection : IDbProvider { }

    public class DbProviderProjection : DbProvider, IDbProviderProjection
    {
        public DbProviderProjection( string connectionStringName ) : base( connectionStringName ) { }
    }
}
