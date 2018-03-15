using System;
using System.Collections.Generic;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceSubscribersTests
    {
        #region Private members

        private EventStoreKitService Service;

        private static readonly List<Message> ProcessedEvents = new List<Message>();

        private class TestEvent1 : DomainEvent {}
        private interface ISubscriber1 : IEventSubscriber { }
        private interface ISubscriber2 : ISubscriber1 { }
        private class Subscriber1 : ISubscriber2
        {
            public void Handle( Message message )
            {
                ProcessedEvents.Add( message );
                MessageHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
                if( message is SequenceMarkerEvent )
                    MessageSequenceHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
            }
            public void Replay( Message message ) {}
            public IEnumerable<Type> HandledEventTypes => new List<Type>{ typeof(TestEvent1) };
            public event EventHandler<MessageEventArgs> MessageHandled;
            public event EventHandler<MessageEventArgs> MessageSequenceHandled;
        }
// ReSharper disable ClassNeverInstantiated.Local
        private class Subscriber2 : Subscriber1{}
// ReSharper restore ClassNeverInstantiated.Local
        
        [SetUp]
        protected void Setup()
        {
            Service = new EventStoreKitService( false );
        }

        [TearDown]
        protected void Teardown()
        {
            Service?.Dispose();
            ProcessedEvents.Clear();
        }

        private TestEvent1 RaiseEvent()
        {
            var id = Guid.NewGuid();
            var msg = new TestEvent1 { Id = id };
            Service.RaiseEvent( msg );
            Service.Wait();
            return msg;
        }
       
        #endregion

        [Test]
        public void ServiceShouldRegisterSubscriberAndConfigureMessageRoutes()
        {
            Service
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();

            var msg = RaiseEvent();

            ProcessedEvents[0].Should().Be( msg );
        }

        [Test]
        public void InitializedServiceShouldRegisterSubscriberAndConfigureMessageRoutes()
        {
            Service.Initialize();
            Service.RegisterEventSubscriber<Subscriber1>();

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
