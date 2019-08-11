using System;
using System.Data.SqlClient;
using NEventStore;
using NEventStore.Conversion;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Serialization.Json;

namespace NESTest
{
    class Program
    {
        static void Main( string[] args )
        {
            var store = Wireup.Init()
                .UsingSqlPersistence( new NetStandardConnectionFactory(SqlClientFactory.Instance, "osbb" ))
                .WithDialect( new MySqlDialect() )
                .InitializeStorageEngine()
                .UsingJsonSerialization()
                .Compress()
                //.EncryptWith(  )
                //.HookIntoPipelineUsing()
                .Build();


        }
    }
}
