using System;
using System.Collections.Generic;
using EventStoreKit.DbProviders;
using EventStoreKit.Services;
using EventStoreKit.Utility;

namespace EventStoreKit.linq2db
{
    public class Linq2DbProviderFactory : IDbProviderFactory
    {
        #region private fields

        private readonly Dictionary<DbConnectionType, Type> ProvidersMap = new Dictionary<DbConnectionType, Type>
        {
            {DbConnectionType.MsSql, typeof(DbProviderMsSql)},
            {DbConnectionType.MySql, typeof(DbProviderMySql)},
            {DbConnectionType.SqlLite, typeof(DbProviderSqlLite)}
        };
        private readonly Func<IDbProvider> DefaultProvider;
        
        #endregion

        public Linq2DbProviderFactory( IDataBaseConfiguration configuration )
        {
            var providerType = ProvidersMap[configuration.DbConnectionType];
            if (!string.IsNullOrWhiteSpace(configuration.ConfigurationString))
            {
                var ctor = providerType.GetConstructor(new[] {typeof(string), typeof(string)});
                if( ctor == null )
                    throw new InvalidOperationException( "There is no appropriate constructor to create DbProvider" );
                DefaultProvider = () => ctor.Invoke(new object[] {configuration.ConfigurationString, null}).OfType<IDbProvider>();
            }
            else
            {
                var ctor = providerType.GetConstructor(new[] {typeof(string), typeof(string)});
                if (ctor == null)
                    throw new InvalidOperationException("There is no appropriate constructor to create DbProvider");
                DefaultProvider = () => ctor.Invoke(new object[] {null, configuration.ConnectionString}).OfType<IDbProvider>();
            }
        }

        /// <inheritdoc />
        public IDbProvider Create()
        {
            return DefaultProvider();
        }
    }
}