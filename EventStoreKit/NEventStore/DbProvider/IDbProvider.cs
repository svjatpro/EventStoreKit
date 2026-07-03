using System.Data;
using System.Linq.Expressions;

namespace EventStoreKit.NEventStore.DbProvider
{
    public interface IDbProvider : IDisposable
    {
        bool CreateTable<T>(bool overwrite = false) where T : class;
        void DropTable<T>() where T : class;
        void TruncateTable<T>() where T : class;

        IQueryable<T> From<T>() where T : class;

        int Count<T>() where T : class;
        int Count<T>(Expression<Func<T, bool>> predicate) where T : class;

        T Single<T>(Expression<Func<T, bool>> predicate) where T : class;
        T? SingleOrDefault<T>(Expression<Func<T, bool>> predicate) where T : class;

        int Delete<T>(Expression<Func<T, bool>> predicate) where T : class;

        int Insert<T>(T entity) where T : class;

        int Insert<TSource, TDestination>(
            Expression<Func<TSource, bool>> predicate,
            Expression<Func<TSource, TDestination>> evaluator)
            where TSource : class
            where TDestination : class;

        int InsertOrReplace<T>(T entity) where T : class;
        int InsertOrUpdate<T>(
            Expression<Func<T>> predicate,
            Expression<Func<T, T?>> evaluator)
            where T : class;

        long InsertBulk<T>(IEnumerable<T> entities) where T : class;

        long InsertBulk<T>(IEnumerable<T> entities, string connectionString)
            where T : class;

        int Update<T>(T entity) where T : class;
        int Update<T>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, T>> evaluator)
            where T : class;

        int ExecuteNonQuery(string query);

        void BeginTransaction(IsolationLevel isolationLevel);
        void CommitTransaction();
        void RollbackTransaction();
    }
}
