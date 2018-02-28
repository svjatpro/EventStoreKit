using System;
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
    public class EventStoreKitServiceCurrentUserProviderTests
    {
        #region Private members

        private EventStoreKitService Service;

        private class Command1 : DomainCommand { }

        private class Event1 : DomainEvent
        {
            public Guid UserId { get; set; }
        }
        private class Aggregate1 : AggregateBase,
            ICommandHandler<Command1>
        {
            public Aggregate1( Guid id )
            {
                Id = id;
                Register<Event1>( msg => {} );
            }
            public void Handle( Command1 cmd )
            {
                RaiseEvent( new Event1{ UserId = cmd.CreatedBy } );
            }
        }
        private class Subscriber1 : EventQueueSubscriber,
            IEventHandler<Event1>
        {
            public Subscriber1( IEventStoreSubscriberContext context ) : base( context ){}
            public void Handle( Event1 message ){}
        }

        [TearDown]
        protected void Teardown()
        {
            Service?.Dispose();
        }

        #endregion

        [Test]
        public void ServiceShouldInitalizeDefaultUserProvider()
        {
            Service = new EventStoreKitService();
            var userProvider = Service.CurrentUserProvider.Value;

            userProvider.Should().Be( Service.CurrentUserProvider.Default );
        }

        [Test]
        public void ServiceShouldUseOverridedUserProvider()
        {
            var overrided = Substitute.For<ICurrentUserProvider>();

            Service = new EventStoreKitService( false );
            Service.CurrentUserProvider.Value = overrided;
            Service.Initialize();

            var actual = Service.CurrentUserProvider.Value;
            actual.Should().Be( overrided );
        }

        [Test]
        public void ServiceShouldUseUserProviderOverridedByMethod()
        {
            var overrided = Substitute.For<ICurrentUserProvider>();

            Service = new EventStoreKitService( false );
            Service
                .SetCurrentUserProvider( overrided )
                .Initialize();

            var actual = Service.CurrentUserProvider.Value;
            actual.Should().Be( overrided );
        }

        [Test]
        public void CurrentUserProviderShouldBeUsedToSetCurrentUserOnEachRaisedEvent()
        {
            var userId = Guid.NewGuid();
            var userProvider = Substitute.For<ICurrentUserProvider>();
            userProvider.CurrentUserId.Returns( userId );

            Service = new EventStoreKitService( false );
            Service
                .SetCurrentUserProvider( userProvider )
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();

            var subscriber = Service.GetSubscriber<Subscriber1>();
            var task = subscriber.When<Event1>( msg => true );

            Service.RaiseEvent( new Event1() );

            task.Wait();
            task.Result.CreatedBy.Should().Be( userId );
        }

        [Test]
        public void CurrentUserProviderShouldBeUsedToSetCurrentUserOnEachPublishedMessages()
        {
            var userId = Guid.NewGuid();
            var userProvider = Substitute.For<ICurrentUserProvider>();
            userProvider.CurrentUserId.Returns( userId );

            Service = new EventStoreKitService( false );
            Service
                .SetCurrentUserProvider( userProvider )
                .RegisterAggregateCommandHandler<Aggregate1>()
                .RegisterEventSubscriber<Subscriber1>()
                .Initialize();

            var subscriber = Service.GetSubscriber<Subscriber1>();
            var task = subscriber.When<Event1>( msg => true );

            Service.SendCommand( new Command1() );

            task.Wait();
            task.Result.CreatedBy.Should().Be( userId ); // auto initialized CreatedBy field in publisehd event
            task.Result.UserId.Should().Be( userId ); // auto initialized CreatedBy field in publisehd command
        }
    }
}
