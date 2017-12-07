using System.Linq;
using LinqToDB;
using LinqToDB.Data;

namespace EventStoreKit.linq2db
{
    public class DbProviderSqlLite : DbProvider
    {
        public DbProviderSqlLite( string configurationString = null, string connectionString = null )
            : base(
                  configurationString != null ? 
                  new DataConnection( configurationString ) :
// ReSharper disable AssignNullToNotNullAttribute
                  new DataConnection( ProviderName.SQLite, connectionString ) )
// ReSharper restore AssignNullToNotNullAttribute
        {
        }

        protected override string GenerateIndexCommand(IndexInfo indexInfo)
        {
            return $"CREATE {(indexInfo.Unique ? "UNIQUE " : "")} INDEX [IX_{indexInfo.TableName}_{indexInfo.IndexName}] ON [{indexInfo.Owner}].[{indexInfo.TableName}] ( {string.Join(", ", indexInfo.Columns.Select(c => $"[{c}] ASC"))} )";
        }
    }
}