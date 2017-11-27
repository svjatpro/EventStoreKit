using System;
using EventStoreKit.Services;

namespace Dummy
{
    class Program
    {
        static void Main()
        {
            // initialize event store service and register command handler and projection
            var service = new EventStoreKitService()
                .RegisterCommandHandler<SpeakerHandler>()
                .RegisterEventSubscriber<GreetingsProjection>();

            // resolve projection
            var projection = service.ResolveSubscriber<GreetingsProjection>();

            // send command
            service.SendCommand( new GreetCommand{ Object = "World" } );
            
            // wait until projection receives and handles message
            projection.WaitMessages();

            // write data, stored in projection
            projection.GetAllMessages().ForEach( m => Console.WriteLine( m.Message ) ); // "Hello, World!"
        }
    }
}
