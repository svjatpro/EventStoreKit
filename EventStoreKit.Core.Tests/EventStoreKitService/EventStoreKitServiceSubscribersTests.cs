using System;
using System.Collections.Generic;
using System.Threading;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceSubscribersTests
    {
        #region Private members

        private EventStoreKitService Service;

        public static List<Message> ProcessedEvents = new List<Message>();

        private class TestEvent1 : DomainEvent { public string Name { get; set; } }
        private class Subscriber1 : IEventSubscriber
        {
            public void Handle( Message message ) { ProcessedEvents.Add( message ); }
            public void Replay( Message message ) {}
            public IEnumerable<Type> HandledEventTypes => new List<Type>{ typeof(TestEvent1) };
            public event EventHandler<SequenceEventArgs> SequenceFinished;
            public event EventHandler<MessageEventArgs> MessageHandled;
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

        private TestEvent1 RaiseEvent()
        {
            var id = Guid.NewGuid();
            var msg = new TestEvent1 { Id = id, Name = "name_" + id };
            Service.RaiseEvent( msg );
            Service.Wait();
            Thread.Sleep( 300 );
            return msg;
        }
       
        #endregion

        [Test]
        public void SetDatabaseShouldMapSubscriversToSingleDb()
        {
            Service
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();

            var msg = RaiseEvent();

            ProcessedEvents[0].Should().Be( msg );
        }
    }
}
