using EventStoreKit.NEventStore.DbProvider;
using System.Reflection;

namespace EventStoreKit.NEventStore.Projections;

public abstract class SqlProjectionBase : EventQueueSubscriber
{
    #region Private fields

    private readonly HashSet<Type> ReadModels = [];

    protected readonly Func<IDbProvider> DbFactory;

    #endregion

    #region Private methods
    
    private void InitReadModel( IDbProvider db, Type modelType )
    {
        var createTableMethod = db.GetType().GetMethod( "CreateTable", BindingFlags.Public | BindingFlags.Instance );
        createTableMethod?.MakeGenericMethod( modelType ).Invoke( db, [false]);
    }

    #endregion

    protected void RegisterReadModel<TReadModel>( bool initializeModel = true )
    {
        RegisterReadModel( typeof( TReadModel ), initializeModel );
    }
    protected void RegisterReadModel( Type tModel, bool initializeModel = true )
    {
        if ( ReadModels.Add( tModel ) && initializeModel )
        {
            DbFactory.Run( db => InitReadModel( db, tModel ) );
        }
    }

    protected SqlProjectionBase( IDbProviderFactory dbProviderFactory ) : this( dbProviderFactory.CreateDbProvider )
    {
    }
    protected SqlProjectionBase( Func<IDbProvider> dbProviderFactory )
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
    protected SqlProjectionBase( IDbProviderFactory dbProviderFactory ) : this( dbProviderFactory.CreateDbProvider )
    {
    }

    protected SqlProjectionBase( Func<IDbProvider> dbProviderFactory ) : base( dbProviderFactory )
    {
        RegisterReadModel<TModel>();
    }
}