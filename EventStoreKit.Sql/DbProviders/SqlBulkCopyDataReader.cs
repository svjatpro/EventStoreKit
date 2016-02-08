using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace EventStoreKit.Sql.PersistanceManager
{
    internal class SqlBulkCopyDataReader<TReadModel> : IDataReader
    {
        #region Useless stuff

        #region Implementation of IDisposable

        public void Dispose() { throw new NotImplementedException(); }

        #endregion

        #region Implementation of IDataRecord

        public string GetDataTypeName( int i ) { throw new NotImplementedException(); }

        public Type GetFieldType( int i ) { throw new NotImplementedException(); }

        public int GetValues( object[] values ) { throw new NotImplementedException(); }

        public bool GetBoolean( int i ) { throw new NotImplementedException(); }

        public byte GetByte( int i ) { throw new NotImplementedException(); }

        public long GetBytes( int i, long fieldOffset, byte[] buffer, int bufferoffset, int length ) { throw new NotImplementedException(); }

        public char GetChar( int i ) { throw new NotImplementedException(); }

        public long GetChars( int i, long fieldoffset, char[] buffer, int bufferoffset, int length ) { throw new NotImplementedException(); }

        public Guid GetGuid( int i ) { throw new NotImplementedException(); }

        public short GetInt16( int i ) { throw new NotImplementedException(); }

        public int GetInt32( int i ) { throw new NotImplementedException(); }

        public long GetInt64( int i ) { throw new NotImplementedException(); }

        public float GetFloat( int i ) { throw new NotImplementedException(); }

        public double GetDouble( int i ) { throw new NotImplementedException(); }

        public string GetString( int i ) { throw new NotImplementedException(); }

        public decimal GetDecimal( int i ) { throw new NotImplementedException(); }

        public DateTime GetDateTime( int i ) { throw new NotImplementedException(); }

        public IDataReader GetData( int i ) { throw new NotImplementedException(); }

        public bool IsDBNull( int i ) { throw new NotImplementedException(); }

        object IDataRecord.this[int i] { get { throw new NotImplementedException(); } }

        object IDataRecord.this[string name] { get { throw new NotImplementedException(); } }

        #endregion

        #region Implementation of IDataReader

        public void Close() { throw new NotImplementedException(); }

        public DataTable GetSchemaTable() { throw new NotImplementedException(); }

        public bool NextResult() { throw new NotImplementedException(); }

        public int Depth { get { throw new NotImplementedException(); } }

        public bool IsClosed { get { throw new NotImplementedException(); } }

        public int RecordsAffected { get { throw new NotImplementedException(); } }

        #endregion

        #endregion

        #region Private fields

        private readonly IList<string> Fields;
        private readonly IList<PropertyInfo> Properties;
        private readonly Dictionary<string, int> FieldsIndexes;
        private readonly IList<TReadModel> Data;
        private int CurrentRow = -1;

        #endregion

        public int FieldCount { get { return Fields.Count(); } }
        public string GetName( int i ) { return Fields[i]; }
        public int GetOrdinal( string name ) { return FieldsIndexes[name]; }
        public object GetValue( int i ) { return Properties[i].GetValue( Data[CurrentRow], null ); }
        public bool Read()
        {
            if ( ( CurrentRow + 1 ) < Data.Count() )
            {
                CurrentRow++;
                return true;
            }
            return false;
        }

        public SqlBulkCopyDataReader( IEnumerable<string> fields, IList<TReadModel> data )
        {
            var type = typeof( TReadModel );
            Fields = fields.ToList();
            Properties = Fields.Select( type.GetProperty ).ToList();
            FieldsIndexes = Fields.Select( ( field, index ) => new { field, index } ).ToDictionary( f => f.field, f => f.index );
            Data = data;
        }
    }
}