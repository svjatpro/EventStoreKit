using System;
using System.Data.SqlClient;
using EventStoreKit.Core.EventStore;
using MySql.Data.MySqlClient;
using NEventStore;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Serialization.Json;
using NUnit.Framework;

namespace EventStoreKit.NEventStore.Tests
{
    [TestFixture]
    public class NEventStoreAdapterTests
    {
        public class Message1 : Message
        {
            public string Text { get; set; }
        }

        [Test]
        public void EventStoreTest()
        {
            var store = Wireup.Init()

                //.UsingSqlPersistence( new MySqlClientFactory(), "neventstore" )
                .UsingSqlPersistence( new MySqlClientFactory(), "Server=127.0.0.1;Port=3306;Database=tmp.tests;Uid=root;Pwd=Jc,,,ktfnm1!;charset=utf8;AutoEnlist=false;" )
                //.UsingSqlPersistence( new NetStandardConnectionFactory( SqlClientFactory.Instance, "neventstore" ) )
                    //"Server=127.0.0.1;Port=3306;Database=dev.tests;Uid=root;Pwd=Vfhszyf1!;charset=utf8;AutoEnlist=false;" ) )
                //.UsingSqlPersistence( new NetStandardConnectionFactory(SqlClientFactory.Instance, "osbb" ))
                .WithDialect( new MySqlDialect() )
                //.UsingInMemoryPersistence()

                //.UsingSqlPersistence( new MySqlClientFactory(), "Server=127.0.0.1;Port=3306;Database=dev.tests;Uid=root;Pwd=Vfhszyf1!;charset=utf8;AutoEnlist=false;" )
                //.WithDialect( new MySqlDialect() )

                .UsingSqlPersistence( new NetStandardConnectionFactory( 
                    SqlClientFactory.Instance, 
                    "data source=localhost;initial catalog = dev.tests; persist security info = True;user id = sa; password = Db,ktfnm1!"))
                .WithDialect( new MsSqlDialect() )
                
                .InitializeStorageEngine()
                .UsingJsonSerialization()
                .Build();

            var id = Guid.NewGuid().ToString();
            var message1 = new Message1 { StreamId = id, Text = "test1" };
            var message2 = new Message1 { StreamId = id, Text = "test2" };

            using ( var stream = store.CreateStream( id ) )
            {
                stream.Add( new EventMessage { Body = message1 } );
                stream.CommitChanges( Guid.NewGuid() );

                stream.Add( new EventMessage { Body = message2 } );
                stream.CommitChanges( Guid.NewGuid() );
            }

            using ( var stream = store.OpenStream( id, 0, int.MaxValue ) )
            {
                foreach ( var @event in stream.CommittedEvents )
                {

                    Console.WriteLine( ( (Message1) @event.Body ).Text );
                }
            }
        }


    }
}