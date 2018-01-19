using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading;
using EventStoreKit.Aggregates;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.linq2db;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using FluentAssertions;
using FluentAssertions.Primitives;
using NEventStore;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceCommandHandlerTests
    {
        #region Private members

        private EventStoreKitService Service;
// ReSharper disable NotAccessedField.Local
        private Subscriber1 Projection1;
// ReSharper restore NotAccessedField.Local

        private class TestEvent1 : DomainEvent { public string Name { get; set; } }
        private class Subscriber1 : EventQueueSubscriber,
            IEventHandler<TestEvent1>
        {
            public Subscriber1( IEventStoreSubscriberContext context ) : base( context ){}

            public readonly ConcurrentBag<DomainEvent> ProcessedEvents = new ConcurrentBag<DomainEvent>();
            public void Handle( TestEvent1 msg )
            {
                ProcessedEvents.Add( msg );
            }
        }
        private class Command1 : DomainCommand { }
        private class Command2 : DomainCommand { }

        private class Aggregate1 : TrackableAggregateBase
        {
            public Aggregate1()
            {
                Register<TestEvent1>( Apply );
            }
            private void Apply( TestEvent1 msg ) { }
            public void Test( Guid id, string name ) {  RaiseEvent( new TestEvent1{ Id = id, Name = name } ); }
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
            Service = new EventStoreKitService();
        }
        
        #endregion

        [Test]
        public void EventStoreShouldBeMappedToSingleDb()
        {
            Service
                .RegisterCommandHandler<CommandHandler1>()
                .RegisterEventSubscriber<Subscriber1>();
            var subscriber = Service.ResolveSubscriber<Subscriber1>();

            Service.SendCommand( new Command1() );
            Service.SendCommand( new Command2() );

            subscriber.WaitMessages();

            subscriber.ProcessedEvents.Count.Should().Be( 2 );
        }

    }
}
