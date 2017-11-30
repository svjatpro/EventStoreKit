﻿using System;
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
            public DbConnectionType SqlClientType { get; set; }
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

        private readonly Func<IDbProvider> DefaultProvider;
        private readonly Dictionary<Type, Func<IDbProvider>> ProvidersMap = new Dictionary<Type, Func<IDbProvider>>();
        
        #endregion

        #region private methods

        private Func<IDbProvider> InitProviderCreator( string configurationString )
        {
            var providerInfo = ResolveProvider( configurationString );
            var ctor = providerInfo.DbProviderType.GetConstructor( new[] { typeof( string ), typeof( string ) } );
            return () => ctor.Invoke( new object[] { configurationString, null } ).OfType<IDbProvider>();
        }
        private Func<IDbProvider> InitProviderCreator( SqlClientType clientType, string connectionString )
        {
            var providerInfo = ResolveProvider( clientType );
            var ctor = providerInfo.DbProviderType.GetConstructor( new[] { typeof( string ), typeof( string ) } );
            return () => ctor.Invoke( new object[] { null, connectionString } ).OfType<IDbProvider>();
        }

        private static ProviderInfo ResolveProvider( string configurationString )
        {
            var providerName = ConfigurationManager.ConnectionStrings[configurationString].ProviderName;
            var providerInfo = Providers.SingleOrDefault( p => p.SqlProviderName == providerName );
            if ( providerInfo == null )
                throw new ArgumentException( "Client is not supported" );

            return providerInfo;
        }

        private static ProviderInfo ResolveProvider( SqlClientType clientType )
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
        public static Type SqlDialectType( SqlClientType clientType )
        {
            var providerInfo = ResolveProvider( clientType );
            return providerInfo.SqlProviderType;
        }

        /// <summary>
        /// Return NEventStore sql dialect type
        /// </summary>
        /// <param name="configurationString"></param>
        /// <exception cref="ArgumentException">Sql client type is not supported</exception>
        public static Type SqlDialectType( string configurationString )
        {
            var providerInfo = ResolveProvider( configurationString );
            return providerInfo.SqlProviderType;
        }
        
        /// <summary>
        /// Constructors receive default configuration string, or if everything exist in single Db, then this is all we need
        /// </summary>
        public DbProviderFactory( string configurationString )
        {
            DefaultProvider = InitProviderCreator( configurationString );
        }

        /// <summary>
        /// Constructors receive default connection string, or if everything exist in single Db, then this is all we need
        /// </summary>
        public DbProviderFactory( SqlClientType clientType, string connectionString )
        {
            DefaultProvider = InitProviderCreator( clientType, connectionString );
        }

        /// <inheritdoc />
        public IDbProvider Create()
        {
            return DefaultProvider();
        }

        /// <inheritdoc />
        public IDbProvider Create<TModel>() where TModel : class
        {
            return ProvidersMap[typeof(TModel)]();
        }

        /// <summary>
        /// if we have several data bases, then we need additionaly map each ( or primary ) model to appropriate DataBase
        /// </summary>
        public DbProviderFactory MapModel<TModel>( string configString )
        {
            ProvidersMap.Add( typeof(TModel), InitProviderCreator( configString ) );
            return this;
        }

        /// <summary>
        /// if we have several data bases, then we need additionaly map each ( or primary ) model to appropriate DataBase
        /// </summary>
        public DbProviderFactory MapModel<TModel>( SqlClientType clientType, string connectionString )
        {
            ProvidersMap.Add( typeof( TModel ), InitProviderCreator( clientType, connectionString ) );
            return this;
        }
       
    }
}
