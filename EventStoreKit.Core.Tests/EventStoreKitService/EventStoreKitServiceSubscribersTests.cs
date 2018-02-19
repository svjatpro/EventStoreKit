using System;
using System.Collections.Generic;
using System.Threading;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using FluentAssertions;
using NSubstitute.ExceptionExtensions;
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
        private interface ISubscriber1 : IEventSubscriber { }
        private interface ISubscriber2 : ISubscriber1 { }
        private class Subscriber1 : ISubscriber2
        {
            public void Handle( Message message ) { ProcessedEvents.Add( message ); }
            public void Replay( Message message ) {}
            public IEnumerable<Type> HandledEventTypes => new List<Type>{ typeof(TestEvent1) };
            public event EventHandler<SequenceEventArgs> SequenceFinished;
            public event EventHandler<MessageEventArgs> MessageHandled;
        }
        private class Subscriber2 : Subscriber1{}
        
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
        public void ServerShouldRegisterSubscriberAndConfigureMessageRoutes()
        {
            Service
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();

            var msg = RaiseEvent();

            ProcessedEvents[0].Should().Be( msg );
        }

        [Test]
        public void SubscriberShouldBeRegisteredInServiceAsAllImplementedSubscriberInterfaces()
        {
            Service
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();

            var subscriber = Service.GetSubscriber<Subscriber1>();
            Service.GetSubscriber<ISubscriber1>().Should().Be( subscriber );
            Service.GetSubscriber<ISubscriber2>().Should().Be( subscriber );
        }

        [Test]
        public void SubscriberInterfacesShouldBeRegisteredAsUnique()
        {
            Assert.Throws( 
                typeof( InvalidOperationException ), 
                () => Service
                    .RegisterEventSubscriber<Subscriber1>()
                    .RegisterEventSubscriber<Subscriber2>() );
        }
    }
}
