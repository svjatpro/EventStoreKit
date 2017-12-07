using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace EventStoreKit.DbProviders
{
    public interface IDbProvider : IDisposable
    {
        void CreateTable<T>( bool overwrite = false ) where T : class;
        void DropTable<T>() where T : class;
        void TruncateTable<T>() where T : class;
            
        IQueryable<T> Query<T>() where T : class;

        int Count<T>() where T : class;
        int Count<T>( Expression<Func<T, bool>> predicat ) where T : class;

        T Single<T>( Expression<Func<T, bool>> predicat ) where T : class;

        int Delete<T>( Expression<Func<T, bool>> predicat ) where T : class;

        int Insert<T>( T entity ) where T : class;
        int Insert<TSource, TDestination>(Expression<Func<TSource, bool>> predicat, Expression<Func<TSource, TDestination>> evaluator)
            where TSource : class
            where TDestination : class;

        int InsertOrReplace<T>( T entity ) where T : class;

        long InsertBulk<T>( IEnumerable<T> entities ) where T : class;
        long InsertBulk<T>( IEnumerable<T> entities, string connectionString ) where T : class;
        
        int Update<T>( Expression<Func<T, bool>> predicat, Expression<Func<T, T>> evaluator ) where T : class;
      
        int ExecuteNonQuery(string query);
        
        void BeginTransaction( IsolationLevel isolationLevel );
        void CommitTransaction();
        void RollbackTransaction();
    }
}
