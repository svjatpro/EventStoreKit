using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace EventStoreKit.DbProviders
{
    public class DbProviderProxy : IDbProvider
    {
        #region Private members

        private readonly Func<IDbProvider> PersistanceManagerBuilder;
        private IDbProvider InternalInstance;
        private IDbProvider Instance 
        { 
            get
            {
                if ( InternalInstance == null )
                {
                    InternalInstance = PersistanceManagerBuilder();
                    if( UseTransaction )
                        InternalInstance.BeginTransaction( IsolationLevel );
                }
                return InternalInstance;
            } 
        }

        private bool UseTransaction;
        private IsolationLevel IsolationLevel = IsolationLevel.ReadCommitted;

        #endregion

        public DbProviderProxy( Func<IDbProvider> persistanceManagerBuilder ) 
        {
            PersistanceManagerBuilder = persistanceManagerBuilder;
        }

        public void BeginTransaction( IsolationLevel isolationLevel )
        {
            IsolationLevel = isolationLevel;
            UseTransaction = true;
            if ( InternalInstance != null )
                InternalInstance.BeginTransaction( isolationLevel );
        }

        public void CommitTransaction()
        {
            if ( InternalInstance != null )
                InternalInstance.CommitTransaction();
        }

        public void RollbackTransaction()
        {
            if ( InternalInstance != null )
                InternalInstance.RollbackTransaction();
        }


        #region Implementation of IDisposable

        public void Dispose()
        {
            if ( InternalInstance != null )
                InternalInstance.Dispose();
        }

        #endregion

        #region Implementation of IDbProvider

        public void CreateTable<T>( bool overwrite = false ) where T : class { Instance.CreateTable<T>( overwrite ); }

        public void DropTable<T>() where T : class { Instance.DropTable<T>(); }

        public void TruncateTable<T>() where T : class { Instance.TruncateTable<T>(); }

        public string GetTableName<T>() { return Instance.GetTableName<T>(); }

        public IList<string> GetTableFields<T>() { return Instance.GetTableFields<T>(); }

        public IQueryable<T> Query<T>() where T : class { return Instance.Query<T>(); }

        public int Count<T>() where T : class { return Instance.Count<T>(); }

        public int Count<T>( Expression<Func<T, bool>> predicat ) where T : class { return Instance.Count( predicat ); }

        public T Single<T>( Expression<Func<T, bool>> predicat ) where T : class { return Instance.Single( predicat ); }

        public int Delete<T>( Expression<Func<T, bool>> predicat ) where T : class { return Instance.Delete( predicat ); }

        public int Insert<T>( T entity ) where T : class { return Instance.Insert( entity ); }

        public int InsertOrReplace<T>( T entity ) where T : class { return Instance.InsertOrReplace( entity ); }

        public long InsertBulk<T>( IEnumerable<T> entities ) where T : class { return Instance.InsertBulk( entities ); }

        public long InsertBulk<T>( IEnumerable<T> entities, string connectionString ) 
            where T : class { return Instance.InsertBulk( entities, connectionString ); }

        public int Insert<T>( Expression<Func<T, bool>> predicat, Expression<Func<T, T>> evaluator )
            where T : class { return Instance.Insert<T>( predicat, evaluator ); }

        public int Insert<TSource, TDestination>( Expression<Func<TSource, bool>> predicat, Expression<Func<TSource, TDestination>> evaluator )
            where TSource : class where TDestination : class
        {
            return Instance.Insert( predicat, evaluator );
        }

        public int Insert<TQuery, TSource, TDestination>( Func<IQueryable<TQuery>, IQueryable<TSource>> converter,
            Expression<Func<TSource, TDestination>> evaluator )
            where TQuery : class where TSource : class where TDestination : class
        {
            return Instance.Insert( converter, evaluator );
        }

        public int Update<T>( Expression<Func<T, bool>> predicat, Expression<Func<T, T>> evaluator )
            where T : class
        {
            return Instance.Update( predicat, evaluator );
        }

        public int Update<TSource, TDestination>( IQueryable<TSource> source,
            Expression<Func<TSource, TDestination>> evaluator )
            where TSource : class where TDestination : class
        {
            return Instance.Update( source, evaluator );
        }

        public int ExecuteNonQuery( string query ) { return Instance.ExecuteNonQuery( query ); }

        #endregion
    }
}
