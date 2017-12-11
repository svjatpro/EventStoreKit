using System;
using EventStoreKit.DbProviders;

namespace EventStoreKit.Services
{
    public interface IDataBaseConfiguration
    {
        DbConnectionType DbConnectionType { get; }
        string ConnectionProviderName { get; }
        string ConfigurationString { get; }
        string ConnectionString { get; }
        Type DbProviderFactoryType { get; }
    }
}