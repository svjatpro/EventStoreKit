using System;
using System.Collections.Generic;
using System.Net.Configuration;
using System.Threading;
using CommonDomain.Core;
using CommonDomain.Persistence;
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
    public class EventStoreKitServiceSagasTests
    {
        #region Private members

        private EventStoreKitService Service;

        public static List<Message> ProcessedEvents = new List<Message>();

        private class TestCommand1 : DomainCommand { public int AltId; }
        private class TestCommand2 : DomainCommand { public int AltId; }
        private class TestCommand3 : DomainCommand { public int AltId; public int Count; }
        private class TestEvent1 : DomainEvent { public int AltId; }
        private class TestEvent2 : DomainEvent { public int AltId; }
        private class TestEvent3 : DomainEvent { public int AltId; public int Count; }

        private class Aggregate1 : AggregateBase,
            ICommandHandler<TestCommand1>,
            ICommandHandler<TestCommand2>,
            ICommandHandler<TestCommand3>
        {
            public Aggregate1( Guid id )
            {
                Id = id;
                Register<TestEvent1>( msg => {} );
                Register<TestEvent2>( msg => {} );
                Register<TestEvent3>( msg => {} );
            }
            public void Handle( TestCommand1 cmd ) { RaiseEvent( new TestEvent1{ Id = cmd.Id, AltId = cmd.AltId } ); }
            public void Handle( TestCommand2 cmd ) { RaiseEvent( new TestEvent2{ Id = cmd.Id, AltId = cmd.AltId } ); }
            public void Handle( TestCommand3 cmd ) { RaiseEvent( new TestEvent3{ Id = cmd.Id, AltId = cmd.AltId, Count = cmd.Count } ); }
        }
        private class Saga1 : SagaBase,
            IEventHandler<TestEvent1>,
            IEventHandler<TestEvent2>
        {
            private int Count;
            public Saga1( string id ) { Id = id; }
            public Saga1( string id, int count ) : this( id ) { Count = count; }

            public void Handle( TestEvent1 message )
            {
                Dispatch( new TestCommand2 { Id = message.Id, AltId = message.AltId } );
            }

            public void Handle( TestEvent2 message )
            {
                Count++;
                Dispatch( new TestCommand3 { Id = message.Id, AltId = message.AltId, Count = Count } );
            }
        }

        //private class SagaHandler1 : SagaEventHandlerBase,
        //    IEventHandler<TestEvent1>
        //{
        //    private readonly ICommandBus CommandBus;
        //    public SagaHandler1( IEventStoreSubscriberContext context, ISagaRepository repository, ICommandBus commandBus ) : 
        //        base( context, repository )
        //    {
        //        CommandBus = commandBus;
        //    }
        //    public void Handle( TestEvent1 message )
        //    {
        //        message.ProcessSaga( new Saga1(), CommandBus );
        //    }
        //}
        private class Subscriber1 : IEventSubscriber,
            IEventHandler<TestEvent3>
        {
            public void Handle( TestEvent3 message ){}
            public void Handle( Message message )
            {
                ProcessedEvents.Add( message );
                MessageHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
                if( message is SequenceMarkerEvent )
                    MessageSequenceHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
            }
            public void Replay( Message message ) {}
            public IEnumerable<Type> HandledEventTypes => new List<Type>{ typeof(TestEvent3) };
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
        public void SagaShouldProcessMessage()
        {
            var id = Guid.NewGuid();

            Service
                .RegisterAggregateCommandHandler<Aggregate1>()
                .RegisterSaga<Saga1>()
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When<TestEvent3>( msg => msg.Id == id );
            Service.SendCommand( new TestCommand1{ Id = id } );
            task.Wait( 1000 );

            ProcessedEvents[0].OfType<TestEvent3>().With( m => m.Id ).Should().Be( id );
        }

        [Test]
        public void SagaIdShouldBeGeneratedByConfiguredRules()
        {
            var id = Guid.NewGuid();
            Service
                .RegisterAggregateCommandHandler<Aggregate1>()
                .RegisterSaga<Saga1>( 
                    sagaIdResolve: SagaId
                        .From<TestEvent1>( msg => $"Saga1_{msg.AltId}" )
                        .From<TestEvent2>( msg => $"Saga1_{msg.AltId}" ) )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When<TestEvent3>( msg => msg.Id == id );
            Service.SendCommand( new TestCommand1 { Id = Guid.NewGuid(), AltId = 1 } );
            Service.SendCommand( new TestCommand1 { Id = Guid.NewGuid(), AltId = 2 } );
            Service.SendCommand( new TestCommand1 { Id = id, AltId = 1 } );
            task.Wait( 1000 );

            ProcessedEvents[2].OfType<TestEvent3>().With( m => m.Count ).Should().Be( 2 );
        }

        [Test]
        public void SagaShouldBeResolvedByCustomFactory()
        {
            var id = Guid.NewGuid();
            Service
                .RegisterAggregateCommandHandler<Aggregate1>()
                .RegisterSaga<Saga1>(
                    sagaIdResolve: SagaId
                        .From<TestEvent1>( msg => $"Saga1_{msg.AltId}" )
                        .From<TestEvent2>( msg => $"Saga1_{msg.AltId}" ),
                    sagaFactory: ( service, sagaId ) => new Saga1( sagaId, 10 ) )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When<TestEvent3>( msg => msg.Id == id );
            Service.SendCommand( new TestCommand1 { Id = Guid.NewGuid(), AltId = 1 } );
            Service.SendCommand( new TestCommand1 { Id = id, AltId = 1 } );
            task.Wait( 1000 );

            ProcessedEvents[1].OfType<TestEvent3>().With( m => m.Count ).Should().Be( 12 );
        }

        // don't save message
        // cache
    }
}
