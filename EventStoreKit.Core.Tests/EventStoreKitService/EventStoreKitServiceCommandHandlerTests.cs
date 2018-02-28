using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CommonDomain.Core;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.Handler;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceCommandHandlerTests
    {
        #region Private members

        private EventStoreKitService Service;
        private Subscriber1 Projection1;

        private class TestEvent1 : DomainEvent { public string Name { get; set; } }
        private class Subscriber1 : EventQueueSubscriber,
            IEventHandler<TestEvent1>
        {
            public Subscriber1( IEventStoreSubscriberContext context ) : base( context ){}

            public readonly ConcurrentBag<TestEvent1> ProcessedEvents = new ConcurrentBag<TestEvent1>();
            public void Handle( TestEvent1 msg )
            {
                ProcessedEvents.Add( msg );
            }
        }
        private class Command1 : DomainCommand { }
        private class Command2 : DomainCommand { }

        private class Aggregate1 : AggregateBase
        {
            public Aggregate1( Guid id )
            {
                Id = id;
                Register<TestEvent1>( Apply );
            }
            private void Apply( TestEvent1 msg ) { }
            public void Test( Guid id, string name ) { RaiseEvent( new TestEvent1{ Id = id, Name = name } ); }
        }

        private class CommandHandler1 :
            ICommandHandler<Command1, Aggregate1>,
            ICommandHandler<Command2, Aggregate1>
        {
            public void Handle( Command1 cmd, CommandHandlerContext<Aggregate1> context )
            {
                context.Entity.Test( cmd.Id, cmd.GetType().Name );
            }
            public void Handle( Command2 cmd, CommandHandlerContext<Aggregate1> context )
            {
                context.Entity.Test( cmd.Id, cmd.GetType().Name );
            }
        }

        [SetUp]
        protected void Setup()
        {
            Service = new EventStoreKitService( false );
        }

        [TearDown]
        protected void Teardown()
        {
            Service?.Dispose();
        }
        
        #endregion

        [Test]
        public void CommandHandlerCanBeRegisterByType()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            Service
                .RegisterCommandHandler<CommandHandler1>()
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var wait = subscriber.When( MessageMatch
                .Is<TestEvent1>( msg => msg.Id == id1 )
                .And<TestEvent1>( msg => msg.Id == id2 ) );
            Service.SendCommand( new Command1{ Id = id1 } );
            Service.SendCommand( new Command2{ Id = id2 } );
            wait.Wait( 1000 );

            var processed = subscriber.ProcessedEvents.OrderBy( e => e.Name ).ToList();
            processed.Count.Should().Be( 2 );
            processed[0].Id.Should().Be( id1 );
            processed[0].Name.Should().Be( typeof( Command1 ).Name );
            processed[1].Id.Should().Be( id2 );
            processed[1].Name.Should().Be( typeof( Command2 ).Name );
        }

        [Test]
        public void CommandHandlerCanBeRegisterByFactory()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            Service
                .RegisterCommandHandler( () => new CommandHandler1() )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var wait = subscriber.When( MessageMatch.Is<TestEvent1>( msg => msg.Id == id1 ).And<TestEvent1>( msg => msg.Id == id2 ) );
            Service.SendCommand( new Command1 { Id = id1 } );
            Service.SendCommand( new Command2 { Id = id2 } );
            wait.Wait( 1000 );

            var processed = subscriber.ProcessedEvents.OrderBy( e => e.Name ).ToList();
            processed.Count.Should().Be( 2 );
            processed[0].Id.Should().Be( id1 );
            processed[0].Name.Should().Be( typeof( Command1 ).Name );
            processed[1].Id.Should().Be( id2 );
            processed[1].Name.Should().Be( typeof( Command2 ).Name );
        }

        [Test]
        public void EventsRaisedByCommandHandlerShouldBeInitializedWithCurrentUserId()
        {
            var userId = Guid.NewGuid();
            var userProvider = Substitute.For<ICurrentUserProvider>();
            userProvider.CurrentUserId.Returns( userId );

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            Service
                .SetCurrentUserProvider( userProvider )
                .RegisterCommandHandler( () => new CommandHandler1() )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var wait = subscriber.When( MessageMatch.Is<TestEvent1>( msg => msg.Id == id1 ).And<TestEvent1>( msg => msg.Id == id2 ) );
            Service.SendCommand( new Command1 { Id = id1 } );
            Service.SendCommand( new Command2 { Id = id2 } );
            wait.Wait( 1000 );

            var processed = subscriber.ProcessedEvents.OrderBy( e => e.Name ).ToList();
            processed[0].CreatedBy.Should().NotBe( default(Guid) );
            processed[0].CreatedBy.Should().Be( processed[1].CreatedBy );
        }

        [Test]
        public void EventsRaisedByCommandHandlerShouldBeInitializedWithCurrentUserIdFromCommand()
        {
            var userId = Guid.NewGuid();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            Service
                .RegisterCommandHandler( () => new CommandHandler1() )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var wait = subscriber.When( MessageMatch.Is<TestEvent1>( msg => msg.Id == id1 ).And<TestEvent1>( msg => msg.Id == id2 ) );
            Service.SendCommand( new Command1 { Id = id1, CreatedBy = userId } );
            Service.SendCommand( new Command2 { Id = id2, CreatedBy = userId } );
            wait.Wait( 1000 );

            var processed = subscriber.ProcessedEvents.OrderBy( e => e.Name ).ToList();
            processed[0].CreatedBy.Should().Be( userId );
            processed[1].CreatedBy.Should().Be( userId );
        }

        [Test]
        public void EventsRaisedByCommandHandlerShouldBeInitializedWithDateTimeFromCommand()
        {
            var time = DateTime.Now.AddDays( -1 );
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            Service
                .RegisterCommandHandler( () => new CommandHandler1() )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var wait = subscriber.When( MessageMatch.Is<TestEvent1>( msg => msg.Id == id1 ).And<TestEvent1>( msg => msg.Id == id2 ) );
            Service.SendCommand( new Command1 { Id = id1, Created = time } );
            Service.SendCommand( new Command2 { Id = id2, Created = time } );
            wait.Wait( 1000 );

            var processed = subscriber.ProcessedEvents.OrderBy( e => e.Name ).ToList();
            processed[0].Created.Should().Be( time );
            processed[1].Created.Should().Be( time );
        }

        [Test]
        public void EventsRaisedByCommandHandlerShouldBeInitializedWithCurrentDateTime()
        {
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            Service
                .RegisterCommandHandler( () => new CommandHandler1() )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var wait = subscriber.When( MessageMatch.Is<TestEvent1>( msg => msg.Id == id1 ).And<TestEvent1>( msg => msg.Id == id2 ) );
            Service.SendCommand( new Command1 { Id = id1 } );
            Service.SendCommand( new Command2 { Id = id2 } );
            wait.Wait( 1000 );

            var processed = subscriber.ProcessedEvents.OrderBy( e => e.Name ).ToList();
            processed[0].Created.Should().BeCloseTo( DateTime.Now, 200 );
            processed[1].Created.Should().BeCloseTo( DateTime.Now, 200 );
        }

    }
}
