using EventStoreKit.NEventStore.DbProvider;
using System.Reflection;

namespace EventStoreKit.NEventStore.Projections;

public abstract class SqlProjectionBase : EventQueueSubscriber
{
    #region Private fields

    private readonly HashSet<Type> ReadModels = [];

    protected readonly IDbProviderFactory DbFactory;

    #endregion

    #region Private methods
    
    private void InitReadModel( IDbProvider db, Type modelType )
    {
        var createTableMethod = db.GetType().GetMethod( "CreateTable", BindingFlags.Public | BindingFlags.Instance );
        createTableMethod?.MakeGenericMethod( modelType ).Invoke( db, new object[] { false } );
    }

    #endregion

    protected void RegisterReadModel<TReadModel>()
    {
        RegisterReadModel( typeof( TReadModel ) );
    }
    protected void RegisterReadModel( Type tModel )
    {
        if ( ReadModels.Add( tModel ) )
        {
            DbFactory.Run( db => InitReadModel( db, tModel ) );
        }
    }
        
    protected SqlProjectionBase(IDbProviderFactory dbProviderFactory)
    {
        DbFactory = dbProviderFactory;
    }

    public List<Type> GetReadModels => ReadModels.ToList();
}

/// <summary>
/// TModel can be not single model, owned by the projection, but this is primary Model class / table
/// </summary>
public abstract class SqlProjectionBase<TModel> : SqlProjectionBase where TModel : class
{
    protected SqlProjectionBase(IDbProviderFactory dbProviderFactory) : base(dbProviderFactory)
    {
        RegisterReadModel<TModel>();
    }
}