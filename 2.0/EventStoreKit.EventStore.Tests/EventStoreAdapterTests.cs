using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace EventStoreKit.EventStore.Tests
{
    public class EventStoreAdapterTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public async Task Test1()
        {
            var connection = EventStoreConnection.Create( new IPEndPoint( IPAddress.Loopback, 1113 ) );
            // Don't forget to tell the connection to connect!
            //connection.ConnectAsync().Wait();


            var event1 = new EventData( Guid.NewGuid(), "type", true, new byte[0], new byte[0] );
            var login = new UserCredentials( "admin", "Cnjhyf1!" );

            // write to stream
            await connection.AppendToStreamAsync( "qwer", ExpectedVersion.Any, event1 );
            await connection.AppendToStreamAsync( "qwer", ExpectedVersion.Any, login, event1 );

            await connection.ConditionalAppendToStreamAsync( "qwer", 123, new[] { event1 } );
            await connection.ConditionalAppendToStreamAsync( "qwer", 123, new[] { event1 }, login );

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