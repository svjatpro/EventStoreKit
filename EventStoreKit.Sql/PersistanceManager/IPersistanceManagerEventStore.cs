
namespace EventStoreKit.Sql.PersistanceManager
{
    public interface IPersistanceManagerEventStore : IPersistanceManager{}

    public class PersistanceManagerEventStore : PersistanceManager, IPersistanceManagerEventStore
    {
        public PersistanceManagerEventStore( string connectionStringName ) : base( connectionStringName ) { }
    }
}
