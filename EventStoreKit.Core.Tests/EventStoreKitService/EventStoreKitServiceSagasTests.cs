using System;
using System.Collections.Generic;
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

        private class TestCommand1 : DomainCommand {}
        private class TestCommand2 : DomainCommand {}
        private class TestEvent1 : DomainEvent {}
        private class TestEvent2 : DomainEvent {}

        private class Aggregate1 : AggregateBase,
            ICommandHandler<TestCommand1>,
            ICommandHandler<TestCommand2>
        {
            public Aggregate1( Guid id )
            {
                Register<TestEvent1>( msg => {} );
                Register<TestEvent2>( msg => {} );
            }
            public void Handle( TestCommand1 cmd ) { RaiseEvent( new TestEvent1{ Id = cmd.Id } ); }
            public void Handle( TestCommand2 cmd ) { RaiseEvent( new TestEvent2{ Id = cmd.Id } ); }
        }
        private class Saga1 : SagaBase<Message>,
            IEventHandler<TestEvent1>
        {
            //public Saga1()
            //{
            //    Register<TestEvent1>( msg => Dispatch( new TestCommand2{ Id = msg.Id } ) );
            //}
            public Saga1( string id ) { }

            public void Handle( TestEvent1 message )
            {
                
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
            IEventHandler<TestEvent2>
        {
            public void Handle( TestEvent2 message ){}
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
                //.RegisterSaga<Saga1>( msg => $"Saga1_{msg.Id}", ( service, sagaId ) => new Saga1( sagaId ) )
                .RegisterSaga<Saga1>( ( service, message ) => new Saga1( $"Saga1_{message.Id}" ) )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();
            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When<TestEvent2>( msg => msg.Id == id );
            Service.SendCommand( new TestCommand1{ Id = id } );
            task.Wait( 1000 );

            ProcessedEvents[0].OfType<TestEvent2>().With( m => m.Id ).Should().Be( id );
        }
    }
}
