using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using EventStoreKit.DbProviders;

namespace EventStoreKit.Services
{
    public class DataBaseConfiguration : IDataBaseConfiguration
    {
        public DbConnectionType DbConnectionType { get; set; }
        public string ConnectionProviderName { get; set; }
        public string ConfigurationString { get; set; }
        public string ConnectionString { get; set; }

        #region Static members

        private class DbConnectionInfo
        {
            public DbConnectionType DbConnectionType { get; set; }
            public string SqlProviderName { get; set; }
        }

        private static readonly List<DbConnectionInfo> DbConnectionMap =
            new List<DbConnectionInfo>
            {
                new DbConnectionInfo
                {
                    DbConnectionType = DbConnectionType.MsSql,
                    SqlProviderName = "System.Data.SqlClient"
                },
                new DbConnectionInfo
                {
                    DbConnectionType = DbConnectionType.MySql,
                    SqlProviderName = "MySql.Data.MySqlClient"
                },
                new DbConnectionInfo
                {
                    DbConnectionType = DbConnectionType.SqlLite,
                    SqlProviderName = "System.Data.SQLite"
                }
            };

        public static IDataBaseConfiguration Initialize( string configurationString )
        {
            var providerName = ConfigurationManager.ConnectionStrings[configurationString].ProviderName;
            var providerInfo = DbConnectionMap.SingleOrDefault( p => p.SqlProviderName == providerName );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            return new DataBaseConfiguration
            {
                DbConnectionType = providerInfo.DbConnectionType,
                ConnectionProviderName = providerInfo.SqlProviderName,
                ConfigurationString = configurationString
            };
        }

        public static IDataBaseConfiguration Initialize( DbConnectionType connectionType, string connectionString )
        {
            var providerInfo = DbConnectionMap.SingleOrDefault( p => p.DbConnectionType == connectionType );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            return new DataBaseConfiguration
            {
                DbConnectionType = providerInfo.DbConnectionType,
                ConnectionProviderName = providerInfo.SqlProviderName,
                ConnectionString = connectionString
            };
        }

        #endregion

    }
}