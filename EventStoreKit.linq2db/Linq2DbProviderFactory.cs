﻿using System;
using System.Collections.Generic;
using EventStoreKit.DbProviders;
using EventStoreKit.Services;
using LinqToDB.DataProvider.SqlServer;

namespace EventStoreKit.linq2db
{
    public class Linq2DbProviderFactory : IDbProviderFactory
    {
        #region private fields

        private class DbProviderBuilderInfo
        {
            public Func<string,IDbProvider> CreateByConfigString { get; set; }
            public Func<string,IDbProvider> CreateByConnectionString { get; set; }
        }
        private readonly Dictionary<DbConnectionType, DbProviderBuilderInfo> ProvidersMap = new Dictionary<DbConnectionType, DbProviderBuilderInfo>
        {
            {
                DbConnectionType.MsSql2000,
                new DbProviderBuilderInfo
                {
                    CreateByConfigString = config => new DbProviderMsSql( config ),
                    CreateByConnectionString = connection => new DbProviderMsSql( SqlServerVersion.v2000, connection ) 
                } 
            },
            {
                DbConnectionType.MsSql2005,
                new DbProviderBuilderInfo
                {
                    CreateByConfigString = config => new DbProviderMsSql( config ),
                    CreateByConnectionString = connection => new DbProviderMsSql( SqlServerVersion.v2005, connection ) 
                }
            },
            {
                DbConnectionType.MsSql2008,
                new DbProviderBuilderInfo
                {
                    CreateByConfigString = config => new DbProviderMsSql( config ),
                    CreateByConnectionString = connection => new DbProviderMsSql( SqlServerVersion.v2008, connection ) 
                } 
            },
            {
                DbConnectionType.MsSql2012,
                new DbProviderBuilderInfo
                {
                    CreateByConfigString = config => new DbProviderMsSql( config ),
                    CreateByConnectionString = connection => new DbProviderMsSql( SqlServerVersion.v2012, connection ) 
                } 
            },
            {
                DbConnectionType.MySql,
                new DbProviderBuilderInfo
                {
                    CreateByConfigString = config => new DbProviderMySql( config ),
                    CreateByConnectionString = connection => new DbProviderMySql( null, connection )
                } 
            },
            {
                DbConnectionType.SqlLite,
                new DbProviderBuilderInfo
                {
                    CreateByConfigString = config => new DbProviderSqlLite( config ),
                    CreateByConnectionString = connection => new DbProviderSqlLite( null, connection )
                } 
            }
        };
        private readonly Func<IDbProvider> DefaultProvider;

        #endregion

        public Linq2DbProviderFactory( IDataBaseConfiguration configuration )
        {
            var providerInitializer = ProvidersMap[configuration.DbConnectionType];
            if (!string.IsNullOrWhiteSpace(configuration.ConfigurationString))
            {
                DefaultProvider = () => providerInitializer.CreateByConfigString( configuration.ConfigurationString );
            }
            else
            {
                DefaultProvider = () => providerInitializer.CreateByConnectionString( configuration.ConnectionString );
            }
        }

        /// <inheritdoc />
        public IDbProvider Create()
        {
            return DefaultProvider();
        }
        
        /// <inheritdoc />
        public IDbProvider Create( IDataBaseConfiguration configuration )
        {
            var providerInitializer = ProvidersMap[configuration.DbConnectionType];
            if( !string.IsNullOrWhiteSpace( configuration.ConfigurationString ) )
            {
                return providerInitializer.CreateByConfigString( configuration.ConfigurationString );
            }
            else
            {
                return providerInitializer.CreateByConnectionString( configuration.ConnectionString );
            }
        }
    }
}