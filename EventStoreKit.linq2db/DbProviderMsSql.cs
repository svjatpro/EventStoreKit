using System.Linq;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;

namespace EventStoreKit.linq2db
{
    public class DbProviderMsSql : DbProvider
    {
        public DbProviderMsSql( string configurationString )
            : base( new DataConnection( configurationString ) )
        {
        }

        public DbProviderMsSql( SqlServerVersion version, string connectionString )
            : base( new DataConnection( new SqlServerDataProvider( ProviderName.SqlServer, version ), connectionString ) )
        {
        }

        protected override string GenerateIndexCommand(IndexInfo indexInfo)
        {
            return $"CREATE {(indexInfo.Unique ? "UNIQUE " : "")}NONCLUSTERED INDEX [IX_{indexInfo.TableName}_{indexInfo.IndexName}] ON [{indexInfo.Owner}].[{indexInfo.TableName}] ( {string.Join(", ", indexInfo.Columns.Select(c => $"[{c}] ASC"))} )";
        }
    }
}