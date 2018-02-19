using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using EventStoreKit.DbProviders;

namespace EventStoreKit.Services
{
    public class DataBaseConfiguration : IDataBaseConfiguration
    {
        public DataBaseConnectionType DataBaseConnectionType { get; }
        public string ConnectionProviderName { get; set; }
        public string ConfigurationString { get; }
        public string ConnectionString { get; }

        #region Static members

        private class DbConnectionInfo
        {
            public DataBaseConnectionType DataBaseConnectionType { get; set; }
            public string SqlProviderName { get; set; }
        }

        private static readonly List<DbConnectionInfo> DbConnectionMap =
            new List<DbConnectionInfo>
            {
                new DbConnectionInfo { DataBaseConnectionType = DataBaseConnectionType.None, SqlProviderName = string.Empty },
                new DbConnectionInfo { DataBaseConnectionType = DataBaseConnectionType.MsSql2000, SqlProviderName = "System.Data.SqlClient" },
                new DbConnectionInfo { DataBaseConnectionType = DataBaseConnectionType.MsSql2005, SqlProviderName = "System.Data.SqlClient" },
                new DbConnectionInfo { DataBaseConnectionType = DataBaseConnectionType.MsSql2008, SqlProviderName = "System.Data.SqlClient" },
                new DbConnectionInfo { DataBaseConnectionType = DataBaseConnectionType.MsSql2012, SqlProviderName = "System.Data.SqlClient" },
                new DbConnectionInfo { DataBaseConnectionType = DataBaseConnectionType.MySql, SqlProviderName = "MySql.Data.MySqlClient" },
                new DbConnectionInfo { DataBaseConnectionType = DataBaseConnectionType.SqlLite, SqlProviderName = "System.Data.SQLite" }
            };

        public static string ResolveSqlProviderName( DataBaseConnectionType connectionType )
        {
            return DbConnectionMap.Single( p => p.DataBaseConnectionType == connectionType ).SqlProviderName;
        }

        #endregion

        public DataBaseConfiguration( string configurationString )
        {
            var providerName = ConfigurationManager.ConnectionStrings[configurationString].ProviderName;
            var providerInfo = DbConnectionMap.SingleOrDefault( p => p.SqlProviderName == providerName );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            DataBaseConnectionType = providerInfo.DataBaseConnectionType;
            ConnectionProviderName = providerInfo.SqlProviderName;
            ConfigurationString = configurationString;
        }

        public DataBaseConfiguration( DataBaseConnectionType connectionType, string connectionString )
        {
            var providerInfo = DbConnectionMap.SingleOrDefault( p => p.DataBaseConnectionType == connectionType );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            DataBaseConnectionType = providerInfo.DataBaseConnectionType;
            ConnectionProviderName = providerInfo.SqlProviderName;
            ConnectionString = connectionString;
        }
        
        public override int GetHashCode()
        {
            return $"{( !string.IsNullOrWhiteSpace( ConfigurationString ) ? ConfigurationString : DataBaseConnectionType + "." + ConnectionString )}"
                    .GetHashCode();
        }
    }
}