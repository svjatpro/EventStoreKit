using System.Data;
using BLToolkit.Data;

namespace EventStoreKit.Sql.PersistanceManager
{
    public class OSMDDbManager : DbManager
    {
        protected override IDbCommand OnInitCommand( IDbCommand command )
        {
            command = base.OnInitCommand( command );
            command.CommandTimeout = 60 * 2;
            return command;
        }
        public OSMDDbManager( string configName ) : base( configName ){}
    }
}