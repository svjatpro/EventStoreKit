using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using CommonDomain.Core;
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
        public void Handle( GreetedEvent message )
        {
            DbProviderFactory.Run( db => db.Insert( 
                new GreetingModel
                {
                    SpeakerId = message.Id,
                    Message = message.HelloMessage
                } ) );
        }

        public GreetingsProjection( ILogger logger, IScheduler scheduler, IEventStoreConfiguration config, IDbProviderFactory dbProviderFactory ) : 
            base( logger, scheduler, config, dbProviderFactory )
        {
        }

        public List<GreetingModel> GetAllMessages()
        {
            return DbProviderFactory.Run( db => db.Query<GreetingModel>().ToList() );
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var service = new EventStoreKitService();
            service.RegisterCommandHandler<SpeakerHandler>();
            service.Initialize();


        }
    }
}
