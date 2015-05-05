using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using BLToolkit.Data;
using BLToolkit.Data.Linq;
using BLToolkit.Data.Sql;

namespace EventStoreKit.Sql.PersistanceManager
{
    public class PersistanceManager : IPersistanceManager
    {
        #region Private fields

        private readonly DbManager DbManager;
        private readonly IQueryComposer QueryComposer;

        #endregion

        public PersistanceManager( string connectionStringName )
        {
            DbManager = new OSMDDbManager( connectionStringName );
            QueryComposer = new MSSQLQueryComposer();
        }

        #region Implementation of IPersistanceManager

        #region Infrastructure

        public void CreateDataBase( string connectionStringName, string dataBaseName )
        {
            using ( var db = new OSMDDbManager( connectionStringName ) )
            {
                db.SetCommand( QueryComposer.CreateDataBase( dataBaseName ) );
                db.ExecuteNonQuery();
            }
        }

        public void DropTable<T>() where T : class
        {
            var table = new SqlTable<T>();
            DropTable( table.Name );
        }

        public void DropTable( string tableName )
        {
            DbManager.SetCommand( QueryComposer.DropTable(tableName) );
            DbManager.ExecuteNonQuery();
        }

        public void CreateTable<T>( bool overwrite = false ) where T : class
        {
            if (overwrite)
                DropTable<T>();

            var table = new SqlTable<T>();
            var createScript = QueryComposer.CreateTable(table);

            DbManager.SetCommand( createScript ).ExecuteNonQuery();
            foreach (var createIndexScript in QueryComposer.CreateIndices(table))
            {
                DbManager.SetCommand( createIndexScript ).ExecuteNonQuery();
            }
        }

        public void TruncateTable<T>() where T : class
        {
            var table = new SqlTable<T>();
            TruncateTable( table.Name, table.Database, table.Owner );
        }

        public void TruncateTable( string table, string database = null, string owner = null )
        {
            if ( string.IsNullOrWhiteSpace( database ) )
                database = DbManager.Connection.Database;
            if ( string.IsNullOrWhiteSpace( owner ) )
                owner = "dbo";
            var truncateQuery = QueryComposer.TruncateTable(table, database, owner);
            DbManager.SetCommand( truncateQuery ).ExecuteNonQuery();
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

        public void Delete<T>( Expression<Func<T, bool>> predicat ) where T : class
        {
            DbManager.GetTable<T>().Where( predicat ).Delete();
        }

        public void Insert<T>( T entity ) where T : class
        {
            DbManager.Insert( entity );
        }
        public void InsertBatch<T>( IEnumerable<T> entities ) where T : class
        {
            DbManager.InsertBatch( entities );
        }
        public void InsertOrReplace<T>( T entity ) where T : class
        {
            DbManager.InsertOrReplace( entity );
        }

        public void InsertBulk<T>( IEnumerable<T> entities ) where T : class
        {
            //InsertBulk( entities, ConfigurationManager.ConnectionStrings[ProjectionsConfigName].ConnectionString );
            InsertBulk( entities, DbManager.Connection.ConnectionString );
        }
        public void InsertBulk<T>( IEnumerable<T> entities, string connectionString ) where T : class
        {
            var dataReader = new SqlBulkCopyDataReader<T>( GetTableFields<T>(), entities.ToList() );
            using ( var bulkCopy = new SqlBulkCopy( connectionString ) )
            {
                bulkCopy.DestinationTableName = GetTableName<T>();
                bulkCopy.WriteToServer( dataReader );
            }
        }

        public void Insert<T>( Expression<Func<T, bool>> predicat, Expression<Func<T, T>> evaluator ) where T : class
        {
            Insert<T,T>( predicat, evaluator );
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
        public void Insert<TQuery, TSource, TDestination>(
            //Expression<Func<TQuery, bool>> predicat,
            Func<IQueryable<TQuery>, IQueryable<TSource>> converter,
            Expression<Func<TSource, TDestination>> evaluator )
            where TQuery : class
            where TSource : class
            where TDestination : class
        {
            //var query = DbManager.GetTable<TQuery>(); //.Where( predicat );
            //var source = converter( query );
            //source.Insert( DbManager.GetTable<TDestination>(), evaluator );
        }

        public void Update<T>( Expression<Func<T, bool>> predicat, Expression<Func<T, T>> evaluator ) where T : class
        {
            DbManager.GetTable<T>().Update( predicat, evaluator );
        }

        public void Update<TSource,TDestination>( IQueryable<TSource> source, Expression<Func<TSource,TDestination>> evaluator )
            where TSource : class 
            where TDestination : class
        {
            source.Update( DbManager.GetTable<TDestination>(), evaluator );
        }

        public int ExecuteNonQuery(string query)
        {
            return DbManager.SetCommand(query).ExecuteNonQuery();
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