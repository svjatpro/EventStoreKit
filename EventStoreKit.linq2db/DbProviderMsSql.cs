using System.Linq;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;

namespace EventStoreKit.linq2db
{
    public class DbProviderMsSql : DbProvider
    {
        public DbProviderMsSql( string configurationString = null, string connectionString = null )
            : base(
                  configurationString != null ? 
                  new DataConnection( configurationString ) :
                  new DataConnection( new SqlServerDataProvider( ProviderName.SqlServer, SqlServerVersion.v2008 ), connectionString ) )
        {
        }

        protected override bool TableExist<T>()
        {
            var command = string.Format("SELECT count(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U')", GetTableName<T>());
            var cmd = DbManager.CreateCommand();
            cmd.CommandText = command;
            return (int)cmd.ExecuteScalar() > 0;
        }

        protected override string GenerateIndexCommand(IndexInfo indexInfo)
        {
            return string.Format("CREATE {0}NONCLUSTERED INDEX [IX_{1}_{2}] ON [{3}].[{4}] ( {5} )",
                indexInfo.Unique ? "UNIQUE " : "",
                indexInfo.TableName,
                indexInfo.IndexName,
                indexInfo.Owner,
                indexInfo.TableName,
                string.Join(", ", indexInfo.Columns.Select(c => string.Format("[{0}] ASC", c)))
            );
        }
    }
}