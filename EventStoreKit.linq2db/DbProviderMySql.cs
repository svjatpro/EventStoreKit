using System;
using System.Linq;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.SqlQuery;

namespace EventStoreKit.linq2db
{
    public class DbProviderMySql : DbProvider
    {
        public DbProviderMySql( string configurationString = null, string connectionString = null ) : 
            base(
                configurationString != null ?
                new DataConnection( configurationString ) :
// ReSharper disable AssignNullToNotNullAttribute
                new DataConnection( ProviderName.MySql, connectionString ) )
// ReSharper restore AssignNullToNotNullAttribute
        {
            // According to http://dev.mysql.com/doc/connector-net/en/connector-net-connection-options.html
            //  "This option was introduced in Connector/Net 6.1.1. 
            //   The backend representation of a GUID type was changed from BINARY(16) to CHAR(36). 
            //   This was done to allow developers to use the server function UUID() to populate a GUID table - UUID() generates a 36-character string. 
            //   Developers of older applications can add 'Old Guids=true' to the connection string to use a GUID of data type BINARY(16)."
            DbManager.MappingSchema.SetDataType( typeof (Guid), new SqlDataType( DataType.Char, 36 ) );
            DbManager.MappingSchema.SetDataType( typeof (Guid?), new SqlDataType( DataType.Char, 36 ) );
        
            // By default all string columns is "Text" (65535) or "TYNYTEXT" (256), according to its length
            //  to change column data type use DataType property in Column attribute
            // Please note, that indexes can't be applied to TEXT columns, 
            //  so use [Column( DataType.NVarChar)] for string columns to use with [FieldIndex] attribute.
            //  also it will not break other DB mapping, because NVarChar is default data type for string columns in MsSql
            DbManager.MappingSchema.SetDataType( typeof(string), DataType.Text );
        }

        protected override string GenerateIndexCommand(IndexInfo indexInfo)
        {
            return $"CREATE {(indexInfo.Unique ? "UNIQUE " : "")} INDEX IX_{indexInfo.TableName}_{indexInfo.IndexName} ON {indexInfo.TableName} ( {string.Join(", ", indexInfo.Columns.Select(c => string.Format("{0} ASC", c)))} )";
        }
    }
}