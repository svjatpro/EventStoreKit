
using OSMD.Common;

namespace OSMD.Domain
{
    public interface IPersistanceManagerInitialData : IPersistanceManager
    {
        
    }
    internal class PersistanceManagerInitialData : PersistanceManager, IPersistanceManagerInitialData
    {
        public PersistanceManagerInitialData( string connectionStringName )
            : base( connectionStringName )
        {
        }
    }
}
