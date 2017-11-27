using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using EventStoreKit.Aggregates;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;

namespace Dummy
{
    public class GreetCommand : DomainCommand
    {
        public string Object { get; set; }
    }

    public class GreetedEvent : DomainEvent
    {
        public string HelloMessage { get; set; }
    }

    public class Speaker : TrackableAggregateBase
    {
        private void Apply( GreetedEvent message ){}

        public Speaker( Guid id )
        {
            Id = id;
            Register<GreetedEvent>( Apply );
        }

        public void Greet( string objectName )
        {
            RaiseEvent( new GreetedEvent
            {
                Id = Id,
                HelloMessage = $"Hello, {objectName}!"
            } );
        }
    }

    public class SpeakerHandler : ICommandHandler<GreetCommand, Speaker>
    {
        public void Handle( GreetCommand cmd, CommandHandlerContext<Speaker> context )
        {
            context.Entity.Greet( cmd.Object );
        }
    }

    public class GreetingModel
    {
        public Guid SpeakerId { get; set; }
        public string Message { get; set; }
    }

    public class GreetingsProjection : SqlProjectionBase<GreetingModel>,
        IEventHandler<GreetedEvent>
    {
        public GreetingsProjection(IEventStoreSubscriberContext context) : base(context)
        {
        }

        public void Handle( GreetedEvent message )
        {
            DbProviderFactory.Run( db =>
                db.Insert(
                    new GreetingModel
                    {
                        SpeakerId = message.Id,
                        Message = message.HelloMessage
                    })
            );
        }
        
        public List<GreetingModel> GetAllMessages()
        {
            return DbProviderFactory.Run( db => db.Query<GreetingModel>().ToList() );
        }
    }

    class Program
    {
        static void Main()
        {
            // initialize event store service with two handlers
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
            projection.GetAllMessages().ForEach( m => Console.WriteLine( m.Message ) );
        }
    }
}
