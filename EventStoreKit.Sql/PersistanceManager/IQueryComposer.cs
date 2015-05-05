using System.Collections.Generic;
using BLToolkit.Data.Sql;

namespace EventStoreKit.Sql.PersistanceManager
{
    public interface IQueryComposer
    {
        string CreateDataBase( string dbName, string collate = "Latin1_General_CI_AS" );
        string DropTable( string tableName );
        string CreateColumn( SqlField f );
        string CreatePrimaryKey<T>( SqlTable<T> table );
        string CreateTable<T>( SqlTable<T> table ) where T : class;
        string TruncateTable( string table, string database = null, string owner = null );
        IEnumerable<string> CreateIndices<T>(SqlTable<T> table);
    }
}