using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BLToolkit.Data.Sql;

namespace EventStoreKit.Sql.PersistanceManager
{
    internal class MSSQLQueryComposer : IQueryComposer
    {
        public string CreateDataBase( string dbName, string collate = "Latin1_General_CI_AS" )
        {
            // todo: get collate from current user culture
            var command = string.Format( "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{0}') \n CREATE DATABASE [{0}] COLLATE {1}", dbName, collate );
            return command;
        }

        public string DropTable( string tableName )
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException("tableName");
            return string.Format
            ( 
                "IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U'))\nDROP TABLE [{0}]", 
                tableName
            );
        }

        public string CreateColumn( SqlField f )
        {
            var type = SqlDataType.GetDataType( f.SystemType );
            var dbType = type.SqlDbType.ToString();
            var len =
                type.Length > 0 ?
                string.Format("({0})", type.Length) :
                (type.Precision > 0 ? string.Format("({0},{1})", type.Precision, type.Scale) : "");

            var fieldTypeAttr = f.MemberMapper.MemberAccessor.GetAttribute<FieldTypeAttribute>();             
            if (fieldTypeAttr != null)
            {
                if (!string.IsNullOrEmpty(fieldTypeAttr.TypeName))
                    dbType = fieldTypeAttr.TypeName;
                if (!string.IsNullOrEmpty(fieldTypeAttr.Length))
                    len = string.Format("({0})", fieldTypeAttr.Length);
            }

            return string.Format( "\n\t[{0}]\t{1}{2}\t{3}NULL{4},",
                f.PhysicalName,
                dbType,
                len,
                f.Nullable ? "" : "NOT ",
                f.IsIdentity ? "\t IDENTITY" : "" );
        }

        public string CreatePrimaryKey<T>( SqlTable<T> table )
        {
            var primaryKeyColumns = table.Fields.Values.Where(f => f.IsPrimaryKey).ToList();
            if (primaryKeyColumns.Any())
            {
                var sb = new StringBuilder();
                sb.AppendFormat("\nCONSTRAINT\tPK_{0}\tPRIMARY KEY\tCLUSTERED\t(", table.Name);
                sb.Append(string.Join(", ", primaryKeyColumns.Select(c => c.PhysicalName + " ASC")));
                sb.Append("),");
                return sb.ToString();
            }
            return "";
        }

        public string CreateTable<T>( SqlTable<T> table ) where T : class
        {
            var sb = new StringBuilder();

            sb.AppendFormat( "IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{0}') AND type in (N'U'))\n CREATE TABLE {0}\n(", table.Name );

            foreach ( var field in table.Fields )
            {
                var column = CreateColumn(field.Value);
                sb.Append(column);
            }

            var primaryKey = CreatePrimaryKey(table);

            sb.Append(primaryKey);
            sb.Length -= 1;
            sb.Append( "\n)" );

            return sb.ToString();
        }

        public string TruncateTable( string table, string database = null, string owner = null )
        {
            return string.Format
            ( 
                "IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{2}') AND type in (N'U'))\nTRUNCATE TABLE [{0}].[{1}].[{2}]", 
                database, owner, table 
            );
        }

        private string CreateIndex(IndexInfo indexInfo)
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

        public IEnumerable<string> CreateIndices<T>(SqlTable<T> table)
        {
            if (table == null || !table.Fields.Values.Any())
                return new string[0];

            var indices = new List<IndexInfo>();
            foreach (var pair in table.Fields)
            {
                var field = pair.Value;
                var attribute = field.MemberMapper.MemberAccessor.GetAttribute<FieldIndexAttribute>();
                if (attribute != null)
                {
                    var index = new IndexInfo(table, field, attribute);
                    var existing = indices.FirstOrDefault(i => i.IndexName == index.IndexName);
                    if (existing != null)
                    {
                        existing.Unique |= index.Unique;
                        existing.Columns.AddRange(index.Columns);
                    }
                    else
                    {
                        indices.Add(index);
                    }
                }
            }
            
            return indices.Select(CreateIndex).ToList();            
        }

        class IndexInfo
        {
            internal IndexInfo(SqlTable table, SqlField field, FieldIndexAttribute attribute)
            {
                TableName = table.Name;
                Owner = string.IsNullOrEmpty(table.Owner) ? "dbo" : table.Owner;
                IndexName = attribute.IndexName ?? field.PhysicalName;
                Unique = attribute.Unique;
                Columns = new List<string> { field.PhysicalName };
            }

            public string Owner { get; set; }
            public string TableName { get; set; }
            public string IndexName { get; set; }
            public bool Unique { get; set; }
            public List<string> Columns { get; set; }
        }
    }
}