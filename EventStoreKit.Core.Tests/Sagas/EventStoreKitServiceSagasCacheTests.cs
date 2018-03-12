using System;
using System.Collections.Generic;
using CommonDomain.Core;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.Handler;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceSagasCacheTests
    {
        #region Private members

        private EventStoreKitService Service;

        public static List<Message> ProcessedEvents = new List<Message>();

        private class TestCommand1 : DomainCommand { }
        private class TestCommand2 : DomainCommand { }
        private class TestEvent1 : DomainEvent { }
        private class TestEvent2 : DomainEvent { }

        private class Aggregate1 : AggregateBase,
            ICommandHandler<TestCommand1>,
            ICommandHandler<TestCommand2>
        {
            public Aggregate1( Guid id )
            {
                Id = id;
                Register<TestEvent1>( msg => {} );
                Register<TestEvent2>( msg => {} );
            }

            public void Handle( TestCommand1 cmd ) { RaiseEvent( new TestEvent1 {Id = cmd.Id} ); }
            public void Handle( TestCommand2 cmd ) { RaiseEvent( new TestEvent2 {Id = cmd.Id} ); }
        }
        private class Saga1 : SagaBase, IEventHandler<TestEvent1>
        {
            public static volatile int CreatedCount = 0;

            public Saga1( string id )
            {
                CreatedCount++;
                Id = id;
            }

            public void Handle( TestEvent1 message )
            {
                Dispatch( new TestCommand2 { Id = message.Id } );
            }
        }

        private class Subscriber1 : IEventSubscriber,
            IEventHandler<TestEvent2>
        {
            public void Handle( TestEvent2 message ) {}
            public void Handle( Message message )
            {
                ProcessedEvents.Add( message );
                MessageHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
                if( message is SequenceMarkerEvent )
                    MessageSequenceHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
            }
            public void Replay( Message message ) {}

            public IEnumerable<Type> HandledEventTypes => new List<Type>{ typeof(TestEvent2) };
            public event EventHandler<MessageEventArgs> MessageHandled;
            public event EventHandler<MessageEventArgs> MessageSequenceHandled;
        }
        
        [SetUp]
        protected void Setup()
        {
            Saga1.CreatedCount = 0;
            ProcessedEvents.Clear();
            Service = new EventStoreKitService( false );
        }

        [TearDown]
        protected void Teardown()
        {
            Service?.Dispose();
        }
       
        #endregion
        
        [Test]
        public void ByDefaultSagaInstanceShouldBeCreatedForEachHandledMessage()
        {
            var id = Guid.NewGuid();
            Service
                .RegisterAggregateCommandHandler<Aggregate1>()
                .RegisterSaga<Saga1>()
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When( MessageMatch
                .Is<TestEvent2>( msg => msg.Id == id )
                .And<TestEvent2>( msg => msg.Id == id )
                .And<TestEvent2>( msg => msg.Id == id ) );
            Service.SendCommand( new TestCommand1 { Id = id } );
            Service.SendCommand( new TestCommand1 { Id = id } );
            Service.SendCommand( new TestCommand1 { Id = id } );
            task.Wait( 1000 );

            Saga1.CreatedCount.Should().Be( 3 );
        }

        [Test]
        public void CachedSagaCreatedShouldBeOnce()
        {
            var id = Guid.NewGuid();
            Service
                .RegisterAggregateCommandHandler<Aggregate1>()
                .RegisterSaga<Saga1>( cached: true )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When( MessageMatch
                .Is<TestEvent2>( msg => msg.Id == id )
                .And<TestEvent2>( msg => msg.Id == id )
                .And<TestEvent2>( msg => msg.Id == id ) );
            Service.SendCommand( new TestCommand1 { Id = id } );
            Service.SendCommand( new TestCommand1 { Id = id } );
            Service.SendCommand( new TestCommand1 { Id = id } );
            task.Wait( 1000 );

            Saga1.CreatedCount.Should().Be( 1 );
        }
    }
}
