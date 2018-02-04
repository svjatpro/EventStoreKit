using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace EventStoreKit.DbProviders
{
    public class DbProviderStub : IDbProvider
    {
        #region Private fields
        
        private readonly ConcurrentDictionary<Type, IList> StorageMap;

        public DbProviderStub(ConcurrentDictionary<Type, IList> storageMap )
        {
            StorageMap = storageMap;
        }

        #endregion

        #region Private methods

        private IQueryable<TEntity> QueryableStorage<TEntity>()
        {
            return Storage<TEntity>().AsQueryable();
        }
        private List<TEntity> Storage<TEntity>()
        {
            var typeKey = typeof(TEntity);
            if (!StorageMap.ContainsKey(typeKey))
            {
                StorageMap.TryAdd( typeKey, new List<TEntity>() );
            }
            return (List<TEntity>)(StorageMap[typeKey]);
        }

        #endregion

        public void BeginTransaction( IsolationLevel isolationLevel )
        {
            // transactions are not supported
        }

        public void CommitTransaction()
        {
            // transactions are not supported
        }

        public void RollbackTransaction()
        {
            // transactions are not supported
        }


        #region Implementation of IDisposable

        public void Dispose()
        {
            //StorageMap.Clear();
        }

        #endregion

        #region Implementation of IDbProvider

        public void CreateTable<T>( bool overwrite = false ) where T : class { /* you don't need a table */ }

        public void DropTable<T>() where T : class { /* I'm slowly removing the table */ }

        public void TruncateTable<T>() where T : class { /* truncate'm'all! */ }
        
        public IQueryable<T> Query<T>() where T : class { return QueryableStorage<T>(); }

        public int Count<T>() where T : class { return Storage<T>().Count(); }

        public int Count<T>( Expression<Func<T, bool>> predicat ) where T : class { return QueryableStorage<T>().Count( predicat ); }

        public T Single<T>( Expression<Func<T, bool>> predicat ) where T : class { return QueryableStorage<T>().Single( predicat ); }
        public T SingleOrDefault<T>( Expression<Func<T, bool>> predicat ) where T : class { return QueryableStorage<T>().SingleOrDefault( predicat ); }

        public int Delete<T>(Expression<Func<T, bool>> predicat) where T : class
        {
            var storage = Storage<T>();
            var toDelete = storage
                .AsQueryable()
                .Where( predicat )
                .ToList();
            toDelete.ForEach( e => storage.Remove( e ) );
            return toDelete.Count;
        }

        public int Insert<T>(T entity) where T : class
        {
            Storage<T>().Add( entity );
            return 1;
        }

        public int InsertOrReplace<T>(T entity) where T : class { return Insert( entity ); }

        public long InsertBulk<T>( IEnumerable<T> entities ) where T : class { Storage<T>().AddRange( entities ); return entities.Count(); }

        public long InsertBulk<T>( IEnumerable<T> entities, string connectionString ) where T : class
        {
            return InsertBulk(entities);
        }

        public int Insert<TSource, TDestination>( Expression<Func<TSource, bool>> predicat, Expression<Func<TSource, TDestination>> evaluator )
            where TSource : class where TDestination : class
        {
            var toInsert = QueryableStorage<TSource>()
                .Where(predicat)
                .Select(evaluator)
                .ToList();
            Storage<TDestination>().AddRange(toInsert);
            return toInsert.Count;
        }

        public int Update<T>( Expression<Func<T, bool>> predicat, Expression<Func<T, T>> evaluator )
            where T : class
        {
            var toUpdate = QueryableStorage<T>()
                .Where(predicat)
                .ToList();
            toUpdate.ForEach( e => evaluator.Compile()( e ) );
            return toUpdate.Count;
        }

        public int ExecuteNonQuery( string query ) { return 0; /* lets imaginge we already executed it */ }

        #endregion
    }
}
