using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualBasic.FileIO;

namespace MaxMindDataExtractor
{
    internal class FileBulkCopyDataReader<T> : IDataReader
    {
        #region Useless stuff

        #region Implementation of IDataReader

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

        #region Implementation of IDataRecord

        public void Close() { throw new NotImplementedException(); }

        public DataTable GetSchemaTable() { throw new NotImplementedException(); }

        public bool NextResult() { throw new NotImplementedException(); }

        public int Depth { get { throw new NotImplementedException(); } }

        public bool IsClosed { get { throw new NotImplementedException(); } }

        public int RecordsAffected { get { throw new NotImplementedException(); } }

        #endregion

        #endregion

        #region Private fields

        private readonly Func<string[], T> Activator;
        private readonly int FieldsCount;

        private readonly IList<string> Fields;
        private readonly IList<PropertyInfo> Properties;
        private readonly Dictionary<string, int> FieldsIndexes;

        private TextFieldParser Reader;
        private T CurrentValue;

        private readonly bool Buffered;
        private const int BufferCount = 100000;
        private int BufferIndex = 0;
        
        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            if( Reader != null )
            {
                Reader.Dispose();
                Reader = null;
            }
        }

        #endregion

        public bool EndOfData { get { return Reader.EndOfData; } }
        public int FinishedCount { get; private set; }
        public int FieldCount { get { return FieldsCount; } }
        public string GetName( int i ) { return Fields[i]; }
        public int GetOrdinal( string name ) { return FieldsIndexes[name]; }
        public object GetValue( int i ) { return Properties[i].GetValue( CurrentValue, null ); }
        public bool Read()
        {
            if( !Reader.EndOfData )
            {
                BufferIndex++;
                if( Buffered && BufferIndex >= BufferCount )
                {
                    BufferIndex = 0;
                    return false;
                }
                var line = Reader.ReadFields();
                CurrentValue = Activator( line );
                FinishedCount++;
                return true;
            }
            return false;
        }

        public FileBulkCopyDataReader( string path, Expression<Func<string[], T>> activator, string delimiter = ",", bool buffered = true )
        {
            Buffered = buffered;
            Activator = activator.Compile();
            FinishedCount = 0;

            Reader =
                new TextFieldParser( path )
                {
                    TextFieldType = FieldType.Delimited,
                    Delimiters = new[] {delimiter}
                };
            var firstLine = Reader.ReadFields();

            var body = activator.Body as MemberInitExpression;
            if ( body != null ) // try to get min fields from expression
                FieldsCount = body.Bindings.Count();
            else if ( firstLine != null ) // otherwise try to get it from the first line, if it contains headers
                FieldsCount = firstLine.Length;

            var type = typeof( T );
            Fields = DbHelper.GetFields<T>().ToList();
            Properties = Fields.Select( type.GetProperty ).ToList();
            FieldsIndexes = Fields.Select( ( field, index ) => new { field, index } ).ToDictionary( f => f.field, f => f.index );
        }
    }
}