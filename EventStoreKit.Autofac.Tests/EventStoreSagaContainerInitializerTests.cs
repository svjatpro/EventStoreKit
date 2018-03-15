using System;
using System.Collections.Generic;
using Autofac;
using CommonDomain;
using CommonDomain.Core;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.Core.Sagas;
using EventStoreKit.Handler;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Tests;
using EventStoreKit.Utility;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Autofac.Tests
{
    [TestFixture]
    public class EventStoreSagaContainerInitializerTests : BasicContainerInitializerTests
    {
        #region Private members

        private static readonly List<Message> ProcessedEvents = new List<Message>();

        private class TestCommand1 : DomainCommand { public int AltId; }
        private class TestCommand2 : DomainCommand { public int AltId; }
        private class TestCommand3 : DomainCommand { public int AltId; public int Count; }
        private class SagaCommand1 : DomainCommand { }
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
                Register<TestEvent1>( msg => { } );
                Register<TestEvent2>( msg => { } );
                Register<TestEvent3>( msg => { } );
            }
            public void Handle( TestCommand1 cmd ) { RaiseEvent( new TestEvent1 { Id = cmd.Id, AltId = cmd.AltId } ); }
            public void Handle( TestCommand2 cmd ) { RaiseEvent( new TestEvent2 { Id = cmd.Id, AltId = cmd.AltId } ); }
            public void Handle( TestCommand3 cmd ) { RaiseEvent( new TestEvent3 { Id = cmd.Id, AltId = cmd.AltId, Count = cmd.Count } ); }
        }
        private class Saga1 : SagaBase,
            IEventHandler<TestEvent1>,
            IEventHandler<TestEvent2>,
            ICommandHandler<SagaCommand1>
        {
            private int Count;
            public static volatile int CreatedCount = 0;

            public Saga1( string id )
            {
                Id = id;
                CreatedCount++;
            }
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

            public void Handle( SagaCommand1 cmd )
            {
                Dispatch( new TestCommand1 { Id = cmd.Id } );
            }
        }

        private class Subscriber1 : IEventSubscriber,
            IEventHandler<TestEvent3>
        {
            public void Handle( TestEvent3 message ) { }
            public void Handle( Message message )
            {
                ProcessedEvents.Add( message );
                MessageHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
                if( message is SequenceMarkerEvent )
                    MessageSequenceHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
            }
            public void Replay( Message message ) { }

            public IEnumerable<Type> HandledEventTypes => new List<Type> { typeof( TestEvent3 ) };
            public event EventHandler<MessageEventArgs> MessageHandled;
            public event EventHandler<MessageEventArgs> MessageSequenceHandled;
        }

        [TearDown]
        protected void TearDown()
        {
            Saga1.CreatedCount = 0;
            ProcessedEvents.Clear();
        }

        #endregion

        [Test]
        public void SagaRegisteredByContainerShouldProcessMessage()
        {
            var id = Guid.NewGuid();

            Builder.RegisterType<Aggregate1>();
            Builder.RegisterType<Subscriber1>().SingleInstance();
            Builder.RegisterType<Saga1>();

            InitializeContainer();

            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When<TestEvent3>( msg => msg.Id == id );
            Service.SendCommand( new TestCommand1 { Id = id } );
            task.Wait( 1000 );

            ProcessedEvents[0].OfType<TestEvent3>().With( m => m.Id ).Should().Be( id );
        }

        [Test]
        public void SagaRegisteredByServiceShouldProcessMessage()
        {
            var id = Guid.NewGuid();

            Builder.RegisterType<Aggregate1>();
            Builder.RegisterType<Subscriber1>().SingleInstance();

            InitializeContainer( service => service.RegisterSaga<Saga1>() );

            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When<TestEvent3>( msg => msg.Id == id );
            Service.SendCommand( new TestCommand1 { Id = id } );
            task.Wait( 1000 );

            ProcessedEvents[0].OfType<TestEvent3>().With( m => m.Id ).Should().Be( id );
        }

        [Test]
        public void SagaIdShouldBeGeneratedByConfiguredRules()
        {
            var id = Guid.NewGuid();

            Builder.RegisterType<Aggregate1>();
            Builder.RegisterType<Subscriber1>().SingleInstance();
            Builder
                .RegisterType<Saga1>()
                .WithMetadata( SagaConstants.IdGenerator, SagaId.For<Saga1>()
                    .From<TestEvent1>( msg => $"Saga1_{msg.AltId}" )
                    .From<TestEvent2>( msg => $"Saga1_{msg.AltId}" ) );

            InitializeContainer();
            
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

            Builder.RegisterType<Aggregate1>();
            Builder.RegisterType<Subscriber1>().SingleInstance();
            Builder
                .RegisterType<Saga1>()
                .WithMetadata( SagaConstants.FactoryMethod, 
                    new Func<IEventStoreKitService, string, ISaga>( ( service, sagaId ) => new Saga1( sagaId, 10 ) ) )
                .WithMetadata( SagaConstants.IdGenerator, SagaId.For<Saga1>()
                    .From<TestEvent1>( msg => $"Saga1_{msg.AltId}" )
                    .From<TestEvent2>( msg => $"Saga1_{msg.AltId}" ) );

            InitializeContainer();

            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When<TestEvent3>( msg => msg.Id == id );
            Service.SendCommand( new TestCommand1 { Id = Guid.NewGuid(), AltId = 1 } );
            Service.SendCommand( new TestCommand1 { Id = id, AltId = 1 } );
            task.Wait( 1000 );

            ProcessedEvents[1].OfType<TestEvent3>().With( m => m.Count ).Should().Be( 12 );
        }

        [Test]
        public void ByDefaultSagaInstanceShouldBeCreatedForEachHandledMessage()
        {
            var id = Guid.NewGuid();

            Builder.RegisterType<Aggregate1>();
            Builder.RegisterType<Subscriber1>().SingleInstance();
            Builder.RegisterType<Saga1>().WithMetadata( SagaConstants.Cached, false );
            InitializeContainer();

            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When( MessageMatch
                .Is<TestEvent3>( msg => msg.Id == id )
                .And<TestEvent3>( msg => msg.Id == id )
                .And<TestEvent3>( msg => msg.Id == id ) );
            Service.SendCommand( new TestCommand1 { Id = id } );
            Service.SendCommand( new TestCommand1 { Id = id } );
            Service.SendCommand( new TestCommand1 { Id = id } );
            task.Wait( 1000 );

            Saga1.CreatedCount.Should().Be( 6 );
        }

        [Test]
        public void CachedSagaCreatedShouldBeOnce()
        {
            var id = Guid.NewGuid();

            Builder.RegisterType<Aggregate1>();
            Builder.RegisterType<Subscriber1>().SingleInstance();
            Builder.RegisterType<Saga1>().WithMetadata( SagaConstants.Cached, true );
            InitializeContainer();

            var subscriber = Service.GetSubscriber<Subscriber1>();

            var task = subscriber.When( MessageMatch
                .Is<TestEvent3>( msg => msg.Id == id )
                .And<TestEvent3>( msg => msg.Id == id )
                .And<TestEvent3>( msg => msg.Id == id ) );
            Service.SendCommand( new TestCommand1 { Id = id } );
            Service.SendCommand( new TestCommand1 { Id = id } );
            Service.SendCommand( new TestCommand1 { Id = id } );
            task.Wait( 1000 );

            Saga1.CreatedCount.Should().Be( 1 );
        }
    }
}
