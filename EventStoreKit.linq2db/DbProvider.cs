using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using EventStoreKit.DbProviders;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.SqlQuery;

namespace EventStoreKit.linq2db
{
    public abstract class DbProvider : IDbProvider
    {
        #region Private fields
        
        protected readonly DataConnection DbManager;

        #endregion

        #region Private methods

        protected class IndexInfo
        {
            internal IndexInfo( SqlTable table, SqlField field, FieldIndexAttribute attribute )
            {
                TableName = table.Name;
                Owner = string.IsNullOrEmpty( table.Owner ) ? "dbo" : table.Owner;
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

        protected abstract string GenerateIndexCommand( IndexInfo indexInfo );

        private IEnumerable<string> CreateIndices<T>( SqlTable<T> table )
        {
            if ( table == null || !table.Fields.Values.Any() )
                return new List<string>();

            var indices = new List<IndexInfo>();
            foreach ( var pair in table.Fields )
            {
                var field = pair.Value;
                var attribute = field.ColumnDescriptor.MemberAccessor.GetAttribute<FieldIndexAttribute>();
                if ( attribute != null )
                {
                    var index = new IndexInfo( table, field, attribute );
                    var existing = indices.FirstOrDefault( i => i.IndexName == index.IndexName );
                    if ( existing != null )
                    {
                        existing.Unique |= index.Unique;
                        existing.Columns.AddRange( index.Columns );
                    }
                    else
                    {
                        indices.Add( index );
                    }
                }
            }

            return indices.Select( GenerateIndexCommand ).ToList();
        }

        private void CreateTableIndexes<T>()
        {
            foreach ( var createIndexScript in CreateIndices( new SqlTable<T>() ) )
                DbManager.SetCommand( createIndexScript ).Execute();
        }

        protected abstract bool TableExist<T>();

        #endregion

        public DbProvider( DataConnection dbManager )
        {
            DbManager = dbManager;
        }

        #region Implementation of IDbProvider

        #region Infrastructure

        public void DropTable<T>() where T : class
        {
            try
            {
                DbManager.DropTable<T>();
            }
            catch ( DbException )
            {
            }
        }

        public void CreateTable<T>( bool overwrite = false ) where T : class
        {
            var exist = TableExist<T>();

            if ( overwrite && exist )
                DropTable<T>();

            if ( !exist || overwrite )
            {
                try
                {
                    DbManager.CreateTable<T>();
                    CreateTableIndexes<T>();
                }
                catch ( DbException )
                {
                }
            }
        }

        public void TruncateTable<T>() where T : class
        {
            DbManager.GetTable<T>().Delete();
        }

        public string GetTableName<T>()
        {
            var table = new SqlTable<T>();
            return table.Name;
        }

        public IList<string> GetTableFields<T>()
        {
            var table = new SqlTable<T>();
            return table.Fields.Select( f => f.Key ).ToList();
        }

        #endregion

        public IQueryable<T> Query<T>() where T : class
        {
            return DbManager.GetTable<T>();
        }

        public int Count<T>() where T : class
        {
            return DbManager.GetTable<T>().Count();
        }

        public int Count<T>( Expression<Func<T, bool>> predicat ) where T : class
        {
            return DbManager.GetTable<T>().Count( predicat );
        }

        public T Single<T>( Expression<Func<T, bool>> predicat ) where T : class
        {
            return DbManager.GetTable<T>().SingleOrDefault( predicat );
        }
        
        public int Delete<T>( Expression<Func<T, bool>> predicat ) where T : class
        {
            return DbManager.GetTable<T>().Where( predicat ).Delete();
        }

        public int Insert<T>( T entity ) where T : class
        {
            return DbManager.Insert( entity );
        }
        public int InsertOrReplace<T>( T entity ) where T : class
        {
            return DbManager.InsertOrReplace( entity );
        }

        public long InsertBulk<T>( IEnumerable<T> entities ) where T : class
        {
            return InsertBulk( entities, DbManager.Connection.ConnectionString );
        }
        public long InsertBulk<T>( IEnumerable<T> entities, string connectionString ) where T : class
        {
            return DbManager.BulkCopy( entities ).RowsCopied;
        }

        public int Insert<T>( Expression<Func<T, bool>> predicat, Expression<Func<T, T>> evaluator ) where T : class
        {
            return Insert<T, T>( predicat, evaluator );
        }
        public int Insert<TSource, TDestination>( Expression<Func<TSource, bool>> predicat, Expression<Func<TSource, TDestination>> evaluator )
            where TSource : class
            where TDestination : class
        {
            return DbManager
                .GetTable<TSource>()
                .Where( predicat )
                .Insert( DbManager.GetTable<TDestination>(), evaluator );
        }
        public int Insert<TQuery, TSource, TDestination>(
            Func<IQueryable<TQuery>, IQueryable<TSource>> converter,
            Expression<Func<TSource, TDestination>> evaluator )
            where TQuery : class
            where TSource : class
            where TDestination : class
        {
            throw new NotImplementedException();
            //var query = DbManager.GetTable<TQuery>(); //.Where( predicat );
            //var source = converter( query );
            //source.Insert( DbManager.GetTable<TDestination>(), evaluator );
        }

        public int Update<T>( Expression<Func<T, bool>> predicat, Expression<Func<T, T>> evaluator ) where T : class
        {
            return DbManager.GetTable<T>().Update( predicat, evaluator );
        }

        public int Update<TSource, TDestination>( IQueryable<TSource> source, Expression<Func<TSource, TDestination>> evaluator )
            where TSource : class
            where TDestination : class
        {
            return source.Update( DbManager.GetTable<TDestination>(), evaluator );
        }

        public int ExecuteNonQuery( string query )
        {
            return DbManager.SetCommand( query ).Execute();
        }

        public void BeginTransaction( IsolationLevel isolationLevel )
        {
            DbManager.BeginTransaction( isolationLevel );
        }

        public void CommitTransaction()
        {
            DbManager.CommitTransaction();
        }

        public void RollbackTransaction()
        {
            DbManager.RollbackTransaction();
        }

        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            DbManager.Dispose();
        }

        #endregion
    }
}