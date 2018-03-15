using System;
using System.Collections.Generic;
using CommonDomain.Core;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.Core.Sagas;
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
    public class EventStoreKitServiceSagasTransientTests
    {
        #region Private members

        private EventStoreKitService Service;
        public static List<Message> ProcessedEvents = new List<Message>();

        private class TestCommand1 : DomainCommand { }
        private class TestCommand2 : DomainCommand { }
        private class TestCommand11 : DomainCommand { }
        private class TestCommand12 : DomainCommand { public int Processed; }
        private class TestEvent1 : DomainEvent { }
        private class TestEvent2 : DomainEvent { }
        private class TestEvent11 : DomainEvent { }
        private class TestEvent12 : DomainEvent { public int Processed; }

        private class Aggregate1 : AggregateBase,
            ICommandHandler<TestCommand1>,
            ICommandHandler<TestCommand2>,
            ICommandHandler<TestCommand11>,
            ICommandHandler<TestCommand12>
        {
            public Aggregate1( Guid id )
            {
                Id = id;
                Register<TestEvent1>( msg => {} );
                Register<TestEvent2>( msg => {} );
                Register<TestEvent11>( msg => {} );
                Register<TestEvent12>( msg => {} );
            }
            public void Handle( TestCommand1 cmd ) { RaiseEvent( new TestEvent1{ Id = cmd.Id } ); }
            public void Handle( TestCommand2 cmd ) { RaiseEvent( new TestEvent2{ Id = cmd.Id } ); }
            public void Handle( TestCommand11 cmd ) { RaiseEvent( new TestEvent11{ Id = cmd.Id } ); }
            public void Handle( TestCommand12 cmd ) { RaiseEvent( new TestEvent12{ Id = cmd.Id, Processed = cmd.Processed } ); }
        }
        private class Saga1 : SagaBase,
            IEventHandler<TestEvent1>,
            IEventHandlerTransient<TestEvent11>
        {
            private int Processed;
            public Saga1( string id ) { Id = id; }

            public void Handle( TestEvent1 message )
            {
                Processed++;
                Dispatch( new TestCommand2 { Id = message.Id } );
            }

            public void Handle( TestEvent11 message )
            {
                Processed++;
                Dispatch( new TestCommand12{ Id = message.Id, Processed = Processed } );
            }
        }
        private class Subscriber1 : IEventSubscriber,
            IEventHandler<TestEvent2>,
            IEventHandler<TestEvent12>
        {
            public void Handle( TestEvent2 message ) {}
            public void Handle( TestEvent12 message ) {}

            public void Handle( Message message )
            {
                ProcessedEvents.Add( message );
                MessageHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
                if( message is SequenceMarkerEvent )
                    MessageSequenceHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
            }
            public void Replay( Message message ) {}

            public IEnumerable<Type> HandledEventTypes => new List<Type>{ typeof(TestEvent2), typeof(TestEvent12) };
            public event EventHandler<MessageEventArgs> MessageHandled;
            public event EventHandler<MessageEventArgs> MessageSequenceHandled;
        }
        
        [SetUp]
        protected void Setup()
        {
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
        public void SagaShouldProcessButDontSaveMessage()
        {
            var id = Guid.NewGuid();
            Service
                .RegisterAggregate<Aggregate1>()
                .RegisterSaga<Saga1>()
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task1 = subscriber.When<TestEvent2>( msg => msg.Id == id );
            Service.SendCommand( new TestCommand1 { Id = id } );
            task1.Wait( 100 );

            var task2 = subscriber.When( MessageMatch
                .Is<TestEvent12>( msg => msg.Id == id )
                .And<TestEvent12>( msg => msg.Id == id )
                .And<TestEvent12>( msg => msg.Id == id ) );
            Service.SendCommand( new TestCommand11 { Id = id } );
            Service.SendCommand( new TestCommand11 { Id = id } );
            Service.SendCommand( new TestCommand11 { Id = id } );
            task2.Wait( 1000 );

            ProcessedEvents[0].OfType<TestEvent2>().Id.Should().Be( id );
            ProcessedEvents[1].OfType<TestEvent12>().Processed.Should().Be( 2 );
            ProcessedEvents[2].OfType<TestEvent12>().Processed.Should().Be( 2 );
            ProcessedEvents[3].OfType<TestEvent12>().Processed.Should().Be( 2 );
        }
    }
}
