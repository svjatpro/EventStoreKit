using System;
using System.Net;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using NEventStore;

namespace NESTest
{
    class Program
    {
        static void Main( string[] args )
        {
            var connection = EventStoreConnection.Create( new IPEndPoint( IPAddress.Loopback, 1113 ) );
            // Don't forget to tell the connection to connect!
            //connection.ConnectAsync().Wait();


            var event1 = new EventData( Guid.NewGuid(), "type", true, new byte[0], new byte[0] );
            var login = new UserCredentials( "login", "pw" );

            // write to stream
            connection.AppendToStreamAsync( "qwer", ExpectedVersion.Any, event1 );
            connection.AppendToStreamAsync( "qwer", ExpectedVersion.Any, login, event1 );

            connection.ConditionalAppendToStreamAsync( "qwer", 123, new []{ event1 } );
            connection.ConditionalAppendToStreamAsync( "qwer", 123, new []{ event1 }, login );

            // read from stream
            //connection.GetStreamMetadataAsync( "stream" ).Result.StreamMetadata.;
            connection.ReadStreamEventsForwardAsync( "stream", 0, 1000, false );
            connection.ReadStreamEventsForwardAsync( "stream", 0, 1000, false, login );

            // read all
            connection.ReadAllEventsForwardAsync( Position.Start, 50000, false );
            connection.ReadAllEventsForwardAsync( Position.Start, 50000, false, login );

            // subscribe for events
            //connection.

            // --------------------------------------

            var store = Wireup.Init()
                //.UsingSqlPersistence( new NetStandardConnectionFactory(SqlClientFactory.Instance, "osbb" ))
                //.WithDialect( new MySqlDialect() )
                .UsingInMemoryPersistence()
                .InitializeStorageEngine()
                //.UsingJsonSerialization()
                //.Compress()
                //.EncryptWith(  )
                //.HookIntoPipelineUsing()
                .Build();

            // write to stream
            var stream = store.OpenStream( "stream" );
            stream.UncommittedEvents.Add( new EventMessage() );
            stream.UncommittedEvents.Add( new EventMessage() );
            stream.CommitChanges( Guid.NewGuid() );

            // read from stream
            store.Advanced.GetFrom( "bucketId", "stream", 0, 1000 );

            foreach ( var @event in stream.CommittedEvents )
                // business processing...
                
            // read all
            store.Advanced.GetFrom( "bucket", 0 );

            // subscribe for events

            //store.Advanced.
            
        }
    }
}
