using System.Linq;
using LinqToDB.Data;

namespace EventStoreKit.linq2db
{
    public class DbProviderMsSql : DbProvider
    {
        public DbProviderMsSql(string connectionStringName)
            : base(new DataConnection( connectionStringName) )
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