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
            EventStoreKit.Services.EventStoreKit.RegisterCommandHandler<SpeakerHandler>();
            //EventStoreKit.RegisterProjection<GreetingsProjection>();
            var server = EventStoreKit.Services.EventStoreKit.Initialize();

            //var projection = server.GetProjection<>();



            //ServerKit.Register<TheAggregateHandlers>();
            //ServerKit.Register<TheProjection>();

            //ServerKit.MapEventStoreDb(configString / (SqlClient, connection string ) )
            //ServerKit.MapReadModel(configString / (SqlClient, connection string ) )

            //var server = new ServerKit.Initialize();

            //server.SendCommand( new Command() );
            //// 


            // ------------------------------------------------------
            //var builder = new ContainerBuilder();

            //builder.RegisterModule(new EventStoreModule(DbProviderFactory.SqlDialectType(OsbbConfiguration.EventStoreConfigName), configurationString: OsbbConfiguration.EventStoreConfigName));
            //builder.RegisterModule(new MembershipModule(new MembershipConfiguration(OsbbConfiguration.ProjectionsConfigName)));
            //builder.RegisterModule(new OsbbAccountModule());
            //builder.RegisterModule(new OrganizationModule());
            //builder.RegisterModule(new HouseModule());
            //builder.RegisterModule(new DomainModule());
            //builder.RegisterModule(new ProjectionsModule());
            //builder.RegisterModule(new WebModule());

            //builder.RegisterApiControllers(Assembly.GetExecutingAssembly());
            //builder.RegisterWebApiModelBinders(Assembly.GetExecutingAssembly());

            //var container = builder.Build();
            //config.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            // ------------------------------------------------------
            //var builder = new ContainerBuilder();
            //EventStoreKit....
            //EventStoreKitServiceAutofac.Initialize( ref builder );

            // builder....
            //var container = builder.Build();
            //config.DependencyResolver = new AutofacWebApiDependencyResolver(container);
        }
    }
}
