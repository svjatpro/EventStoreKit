using System;
using EventStoreKit.DbProviders;

namespace EventStoreKit.Services
{
    public interface IDataBaseConfiguration
    {
        DataBaseConnectionType DataBaseConnectionType { get; }
        string ConnectionProviderName { get; }
        string ConfigurationString { get; }
        string ConnectionString { get; }
        //Type DbProviderFactoryType { get; }
    }
}