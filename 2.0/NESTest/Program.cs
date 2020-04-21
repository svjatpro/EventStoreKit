using System;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using EventStoreKit.Core.EventStore;
using EventStoreKit.NEventStore;
using NEventStore;
using NEventStore.Persistence.Sql;

namespace NESTest
{
    public class Message1 : Message
    {
        public string Text { get; set; }
    }
    class Program
    {
        static void Main( string[] args )
        {
            TestEventStore();
        }

        public static void TestEventStore()
        {
            var store = Wireup.Init()
                .UsingSqlPersistence( new NetStandardConnectionFactory( SqlClientFactory.Instance, "osbb" ) )
                //.UsingSqlPersistence( new NetStandardConnectionFactory(SqlClientFactory.Instance, "osbb" ))
                //.WithDialect( new MySqlDialect() )
                .UsingInMemoryPersistence()
                .InitializeStorageEngine()
                //.UsingJsonSerialization()
                //.Compress()
                //.EncryptWith(  )
                //.HookIntoPipelineUsing()
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
                    Console.WriteLine( ((Message1)@event.Body).Text );
                }
            }

            //var store = new NEventStoreAdapter( w => w
            //    .UsingInMemoryPersistence()
            //    .InitializeStorageEngine()
            //    .Build() );

            //store.AppendToStream( id, message1 );
            //store.AppendToStream( id, message2 );

            // -------------------

            //// Read the Store with a Polling Client
            //using ( store )
            //{
            //    Int64 checkpointToken = LoadCheckpoint();
            //    var client = new PollingClient2( store.Advanced, commit =>
            //        {
            //            // Project / Dispatch the commit etc
            //            Console.WriteLine( Resources.CommitInfo, commit.BucketId, commit.StreamId, commit.CommitSequence );
            //            // Track the most recent checkpoint
            //            checkpointToken = commit.CheckpointToken;
            //            return PollingClient2.HandlingResult.MoveToNext;
            //        },
            //        waitInterval: 3000 );
            //    // start the polling client
            //    client.StartFrom( checkpointToken );
            //    // before terminating the execution...
            //    client.Stop();
            //    SaveCheckpoint( checkpointToken );
            //}
        }

        public async Task Test()
        { 
            var connection = EventStoreConnection.Create( new IPEndPoint( IPAddress.Loopback, 1113 ) );
            // Don't forget to tell the connection to connect!
            //connection.ConnectAsync().Wait();


            var event1 = new EventData( Guid.NewGuid(), "type", true, new byte[0], new byte[0] );
            var login = new UserCredentials( "login", "pw" );

            // write to stream
            await connection.AppendToStreamAsync( "qwer", ExpectedVersion.Any, event1 );
            await connection.AppendToStreamAsync( "qwer", ExpectedVersion.Any, login, event1 );

            await connection.ConditionalAppendToStreamAsync( "qwer", 123, new []{ event1 } );
            await connection.ConditionalAppendToStreamAsync( "qwer", 123, new []{ event1 }, login );

            // read from stream
            //connection.GetStreamMetadataAsync( "stream" ).Result.StreamMetadata.;
            var slice = await connection.ReadStreamEventsForwardAsync( "stream", 0, 1000, false );
            //slice.Events.First().Event.
            await connection.ReadStreamEventsForwardAsync( "stream", 0, 1000, false, login );

            // read all
            await connection.ReadAllEventsForwardAsync( Position.Start, 50000, false );
            await connection.ReadAllEventsForwardAsync( Position.Start, 50000, false, login );

            // subscribe for events
            //connection.
        }
    }
}
