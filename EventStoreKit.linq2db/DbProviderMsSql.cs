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
// ReSharper disable AssignNullToNotNullAttribute
                  new DataConnection( new SqlServerDataProvider( ProviderName.SqlServer, SqlServerVersion.v2008 ), connectionString ) )
// ReSharper restore AssignNullToNotNullAttribute
        {
        }

        protected override string GenerateIndexCommand(IndexInfo indexInfo)
        {
            return $"CREATE {(indexInfo.Unique ? "UNIQUE " : "")}NONCLUSTERED INDEX [IX_{indexInfo.TableName}_{indexInfo.IndexName}] ON [{indexInfo.Owner}].[{indexInfo.TableName}] ( {string.Join(", ", indexInfo.Columns.Select(c => $"[{c}] ASC"))} )";
        }
    }
}