using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using EventStoreKit.DbProviders;
using EventStoreKit.Utility;

namespace EventStoreKit.linq2db
{
    public class DbProviderFactory : IDbProviderFactory
    {
        #region private fields

        private class ProviderInfo
        {
            public SqlClientType SqlClientType { get; set; }
            public string SqlProviderName { get; set; }
            public Type SqlProviderType { get; set; }
            public Type DbProviderType { get; set; }
        }

        private static readonly List<ProviderInfo> Providers =
            new List<ProviderInfo>
            {
                new ProviderInfo
                {
                    SqlClientType = SqlClientType.MsSqlClient,
                    SqlProviderName = "System.Data.SqlClient",
                    SqlProviderType = typeof(NEventStore.Persistence.Sql.SqlDialects.MsSqlDialect),
                    DbProviderType = typeof(DbProviderMsSql)
                },
                new ProviderInfo
                {
                    SqlClientType = SqlClientType.MySqlClient,
                    SqlProviderName = "MySql.Data.MySqlClient",
                    SqlProviderType = typeof(NEventStore.Persistence.Sql.SqlDialects.MySqlDialect),
                    DbProviderType = typeof(DbProviderMySql)
                }
            };

        #endregion

        #region private methods

        private ProviderInfo ResolveProvider( string configurationString )
        {
            var providerName = ConfigurationManager.ConnectionStrings[configurationString].ProviderName;
            var providerInfo = Providers.SingleOrDefault( p => p.SqlProviderName == providerName );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            return providerInfo;
        }

        private ProviderInfo ResolveProvider( SqlClientType clientType )
        {
            var providerInfo = Providers.SingleOrDefault( p => p.SqlClientType == clientType );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            return providerInfo;
        }

        #endregion


        /// <summary>
        /// Return NEventStore sql dialect type
        /// </summary>
        /// <param name="clientType"></param>
        /// <exception cref="ArgumentException">Sql client type is not supported</exception>
        public Type SqlDialectType( SqlClientType clientType )
        {
            var providerInfo = ResolveProvider( clientType );
            return providerInfo.SqlProviderType;
        }

        /// <summary>
        /// Return NEventStore sql dialect type
        /// </summary>
        /// <param name="configurationString"></param>
        /// <exception cref="ArgumentException">Sql client type is not supported</exception>
        public Type SqlDialectType( string configurationString )
        {
            var providerInfo = ResolveProvider( configurationString );
            return providerInfo.SqlProviderType;
        }

        /// <summary>
        /// Create DbProvider instance by clientType and connection string
        /// </summary>
        /// <param name="clientType"></param>
        /// <param name="connectionString"></param>
        /// <exception cref="ArgumentException">Sql client type is not supported</exception>
        public IDbProvider CreateByConnectionString( SqlClientType clientType, string connectionString )
        {
            var providerInfo = ResolveProvider( clientType );

            var ctor = providerInfo.DbProviderType.GetConstructor( new [] {typeof(string), typeof(string)} );
            var dbProvider = ctor.Invoke( new object[] {null, connectionString} ).OfType<IDbProvider>();

            return dbProvider;
        }

        /// <summary>
        /// Create DbProvider instance by configuration string
        /// </summary>
        /// <param name="configurationString">configuration string name</param>
        /// <exception cref="ArgumentException">Sql client type is not supported</exception>
        public IDbProvider CreateByConfiguration( string configurationString )
        {
            var providerInfo = ResolveProvider( configurationString );

            var ctor = providerInfo.DbProviderType.GetConstructor( new[] { typeof( string ), typeof( string ) } );
            var dbProvider = ctor.Invoke( new object[] { configurationString, null } ).OfType<IDbProvider>();

            return dbProvider;
        }
    }
}
