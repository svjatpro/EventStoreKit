
namespace EventStoreKit.Sql.PersistanceManager
{
    public interface IPersistanceManagerProjection : IPersistanceManager { }

    public class PersistanceManagerProjection : PersistanceManager, IPersistanceManagerProjection
    {
        public PersistanceManagerProjection( string connectionStringName ) : base( connectionStringName ) { }
    }
}
