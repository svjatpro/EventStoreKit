using System;
using System.Net;
using System.Text;
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
        public async Task TestEventStore()
        {
            var connection = EventStoreConnection.Create( new Uri( "tcp://admin:Cnjhyf1!@localhost:1113" ) );
            await connection.ConnectAsync();

            var stream = Guid.NewGuid().ToString().Substring( 0, 6 ).ToLower();
            var event1 = new EventData(
                Guid.NewGuid(), stream, true, 
                Encoding.UTF8.GetBytes( "{text: 'name1'}" ),
                Encoding.UTF8.GetBytes( "{meta: 'name1'}" ) );
            var event2 = new EventData(
                Guid.NewGuid(), stream, true,
                Encoding.UTF8.GetBytes( "{text: 'name2'}" ),
                Encoding.UTF8.GetBytes( "{meta: 'name2'}" ) );
            
            // write to stream
            await connection.AppendToStreamAsync( stream, ExpectedVersion.Any, event1 );
            //await connection.AppendToStreamAsync( "qwer", ExpectedVersion.Any, login, event1 );

            await connection.ConditionalAppendToStreamAsync( "stream1", 123, new[] { event2 } );
            //await connection.ConditionalAppendToStreamAsync( "qwer", 123, new[] { event1 }, login );

            // read from stream
            //connection.GetStreamMetadataAsync( "stream" ).Result.StreamMetadata.;
            var slice = await connection.ReadStreamEventsForwardAsync( "stream1", 0, 1000, false );
            foreach ( var @event in slice.Events )
            {
                Console.WriteLine( "Read event with data: {0}, metadata: {1}",
                    Encoding.UTF8.GetString( @event.Event.Data ),
                    Encoding.UTF8.GetString( @event.Event.Metadata ) );
            }
            //slice.Events.First().Event.
            //await connection.ReadStreamEventsForwardAsync( "stream", 0, 1000, false, login );

            // read all
            //await connection.ReadAllEventsForwardAsync( Position.Start, 50000, false );
            //await connection.ReadAllEventsForwardAsync( Position.Start, 50000, false, login );

            // subscribe for events
            //connection.
        }
    }
}