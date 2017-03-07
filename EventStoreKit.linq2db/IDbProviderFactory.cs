using System;
using EventStoreKit.DbProviders;

namespace EventStoreKit.linq2db
{
    public interface IDbProviderFactory
    {
        Type SqlDialectType( SqlClientType clientType );
        Type SqlDialectType( string configurationString );
        IDbProvider CreateByConnectionString( SqlClientType clientType, string connectionString );
        IDbProvider CreateByConfiguration( string configurationString );
    }
}