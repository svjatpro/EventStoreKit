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
        public Type DbProviderFactoryType { get; set; }

        #region Static members

        private class DbConnectionInfo
        {
            public DbConnectionType DbConnectionType { get; set; }
            public string SqlProviderName { get; set; }
        }

        private static readonly List<DbConnectionInfo> DbConnectionMap =
            new List<DbConnectionInfo>
            {
                new DbConnectionInfo { DbConnectionType = DbConnectionType.None, SqlProviderName = string.Empty },
                new DbConnectionInfo { DbConnectionType = DbConnectionType.MsSql2000, SqlProviderName = "System.Data.SqlClient" },
                new DbConnectionInfo { DbConnectionType = DbConnectionType.MsSql2005, SqlProviderName = "System.Data.SqlClient" },
                new DbConnectionInfo { DbConnectionType = DbConnectionType.MsSql2008, SqlProviderName = "System.Data.SqlClient" },
                new DbConnectionInfo { DbConnectionType = DbConnectionType.MsSql2012, SqlProviderName = "System.Data.SqlClient" },
                new DbConnectionInfo { DbConnectionType = DbConnectionType.MySql, SqlProviderName = "MySql.Data.MySqlClient" },
                new DbConnectionInfo { DbConnectionType = DbConnectionType.SqlLite, SqlProviderName = "System.Data.SQLite" }
            };

        public static IDataBaseConfiguration Initialize( Type factoryType, string configurationString )
        {
            var providerName = ConfigurationManager.ConnectionStrings[configurationString].ProviderName;
            var providerInfo = DbConnectionMap.SingleOrDefault( p => p.SqlProviderName == providerName );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            return new DataBaseConfiguration
            {
                DbProviderFactoryType = factoryType,
                DbConnectionType = providerInfo.DbConnectionType,
                ConnectionProviderName = providerInfo.SqlProviderName,
                ConfigurationString = configurationString
            };
        }

        public static IDataBaseConfiguration Initialize( Type factoryType, DbConnectionType connectionType, string connectionString )
        {
            var providerInfo = DbConnectionMap.SingleOrDefault( p => p.DbConnectionType == connectionType );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            return new DataBaseConfiguration
            {
                DbProviderFactoryType = factoryType,
                DbConnectionType = providerInfo.DbConnectionType,
                ConnectionProviderName = providerInfo.SqlProviderName,
                ConnectionString = connectionString
            };
        }

        #endregion

        public override int GetHashCode()
        {
            return $"{DbProviderFactoryType.Name}.{( !string.IsNullOrWhiteSpace( ConfigurationString ) ? ConfigurationString : DbConnectionType + "." + ConnectionString )}"
                    .GetHashCode();
        }
    }
}