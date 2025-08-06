using System.Data;

namespace EventStoreKit.NEventStore.DbProvider;

public static class DbExtensions
{
    public static void Run(
        this IDbProviderFactory factory,
        Action<IDbProvider> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        Action<Exception>? processException = null,
        bool transaction = true)
    {
        using var dbProvider = factory.CreateDbProvider();
        try
        {
            if (transaction)
            {
                dbProvider.BeginTransaction(isolationLevel);
            }
            action(dbProvider);
            if (transaction)
            {
                dbProvider.CommitTransaction();
            }
        }
        catch (Exception exc)
        {
            if (transaction)
            {
                dbProvider.RollbackTransaction();
            }
            processException?.Invoke(exc);
            throw;
        }
    }

    // Performs action/method with separate instance of DbProvider within Sql Transaction.
    //   If the result is query result / list, then use ToList(). 
    //   The reason is, that deferred materialization will be failed because of disposed connection ( and committed transaction )
    public static TResult Run<TResult>(
        this IDbProviderFactory factory,
        Func<IDbProvider, TResult> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        Action<Exception>? processException = null)
    {
        using var dbProvider = factory.CreateDbProvider();
        try
        {
            dbProvider.BeginTransaction(isolationLevel);
            var result = action(dbProvider);
            dbProvider.CommitTransaction();
            return result;
        }
        catch (Exception exc)
        {
            dbProvider.RollbackTransaction();
            processException?.Invoke(exc);

            throw;
        }
    }
}
