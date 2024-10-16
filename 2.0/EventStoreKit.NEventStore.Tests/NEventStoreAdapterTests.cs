using System;
using System.Collections;
using System.Data.SqlClient;
using EventStoreKit.Core.EventStore;
using NEventStore;
using NEventStore.Domain;
using NEventStore.Domain.Core;
using NEventStore.Domain.Persistence;
using NEventStore.Domain.Persistence.EventStore;
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

        public class CreateEvent1
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
        }
        public class UpdateEvent1
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
        }


        public class Aggregate1 : AggregateBase
        {
            #region Private members

            private void Apply( UpdateEvent1 msg )
            {
                Name = msg.Name;
            }

            private void Apply( CreateEvent1 msg )
            {
                Name = msg.Name;
            }

            #endregion

            private string Name { get; set; }

            public Aggregate1( Guid id )
            {
                Id = id;
                Register<CreateEvent1>( Apply );
                Register<UpdateEvent1>( Apply );
            }
            public Aggregate1( Guid id, string name ) : this( id )
            {
                RaiseEvent( new CreateEvent1{ Id = id, Name = name } );
            }
            public void Update( string name )
            {
                RaiseEvent( new UpdateEvent1 { Id = Id, Name = name } );
            }
        }

        public class Factory : IConstructAggregates
        {
            public IAggregate Build( Type type, Guid id, IMemento snapshot )
            {
                return Activator.CreateInstance( type, id ) as IAggregate;
            }
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

        [Test]
        public void EventStoreDomainTest()
        {
            var store = Wireup.Init()
                .UsingInMemoryPersistence()

                //.UsingSqlPersistence( new MySqlClientFactory(), "Server=127.0.0.1;Port=3306;Database=dev.tests;Uid=root;Pwd=Vfhszyf1!;charset=utf8;AutoEnlist=false;" )
                //.WithDialect( new MySqlDialect() )

                //.UsingSqlPersistence( new NetStandardConnectionFactory(
                //    SqlClientFactory.Instance,
                //    "data source=localhost;initial catalog = dev.tests; persist security info = True;user id = sa; password = Db,ktfnm1!" ) )
                //.WithDialect( new MsSqlDialect() )

                .InitializeStorageEngine()
                .UsingJsonSerialization()
                .Build();
            var repo = new EventStoreRepository( store, new factory , new ConflictDetector() );

            var id = Guid.NewGuid();
            var name1 = "name1";
            var name2 = "name2";
            var aggr = new Aggregate1( id, name1 );


            
        }

    }
}