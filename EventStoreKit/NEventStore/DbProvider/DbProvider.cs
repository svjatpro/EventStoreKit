using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using LinqToDB;
using LinqToDB.Data;

namespace EventStoreKit.NEventStore.DbProvider
{
    public class DbProvider(DataConnection dbConnection) : IDbProvider
    {
        #region Implementation of IDbProvider

        #region Infrastructure

        public void DropTable<T>() where T : class
        {
            try
            {
                dbConnection.DropTable<T>();
            }
            catch (DbException)
            {
            }
        }

        public bool CreateTable<T>(bool overwrite = false) where T : class
        {
            var sp = dbConnection.DataProvider.GetSchemaProvider();
            var dbSchema = sp.GetSchema(dbConnection);
            var tableExist = dbSchema.Tables
               .Any(t => t.TableName == dbConnection.GetTable<T>().TableName);

            if (tableExist && overwrite)
            {
                DropTable<T>();
            }

            if (!tableExist || overwrite)
            {
                dbConnection.CreateTable<T>();
                return true;
            }

            return false;
        }

        public void TruncateTable<T>() where T : class
        {
            dbConnection.GetTable<T>().Delete();
        }

        public string GetTableName<T>() where T : class
        {
            return dbConnection.GetTable<T>().TableName;
        }

        #endregion

        public IQueryable<T> From<T>() where T : class
        {
            return dbConnection.GetTable<T>();
        }

        public int Count<T>() where T : class
        {
            return dbConnection.GetTable<T>().Count();
        }

        public int Count<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return dbConnection.GetTable<T>().Count(predicate);
        }

        public T Single<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return dbConnection.GetTable<T>().Single(predicate);
        }

        public T SingleOrDefault<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return dbConnection.GetTable<T>().SingleOrDefault(predicate)!;
        }

        public int Delete<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            return dbConnection.GetTable<T>().Where(predicate).Delete();
        }

        public int Insert<T>(T entity) where T : class
        {
            return dbConnection.Insert(entity);
        }

        public int InsertOrReplace<T>(T entity) where T : class
        {
            return dbConnection.InsertOrReplace(entity);
        }

        public long InsertBulk<T>(IEnumerable<T> entities) where T : class
        {
            return InsertBulk(entities, dbConnection.Connection.ConnectionString);
        }

        public long InsertBulk<T>(IEnumerable<T> entities, string connectionString) where T : class
        {
            return dbConnection.BulkCopy(entities).RowsCopied;
        }

        public int Insert<TSource, TDestination>(
            Expression<Func<TSource, bool>> predicate,
            Expression<Func<TSource, TDestination>> evaluator)
            where TSource : class
            where TDestination : class
        {
            return dbConnection
               .GetTable<TSource>()
               .Where(predicate)
               .Insert(dbConnection.GetTable<TDestination>(), evaluator);
        }

        public int Update<T>(T entity) where T : class
        {
            return dbConnection.Update(entity);
        }

        public int Update<T>(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, T>> evaluator)
            where T : class
        {
            return dbConnection.GetTable<T>().Update(predicate, evaluator);
        }

        public int ExecuteNonQuery(string query)
        {
            return dbConnection.SetCommand(query).Execute();
        }

        public void BeginTransaction(IsolationLevel isolationLevel)
        {
            dbConnection.BeginTransaction(isolationLevel);
        }

        public void CommitTransaction()
        {
            dbConnection.CommitTransaction();
        }

        public void RollbackTransaction()
        {
            dbConnection.RollbackTransaction();
        }

        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            dbConnection.Dispose();
        }

        #endregion
    }
}
