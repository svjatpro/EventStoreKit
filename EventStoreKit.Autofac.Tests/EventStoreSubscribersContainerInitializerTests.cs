using System;
using System.Collections.Generic;
using Autofac;
using EventStoreKit.Autofac;
using EventStoreKit.DbProviders;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreSubscribersContainerInitializerTests : BasicContainerInitializerTests
    {
        #region Private members

        private class DbProviderFactory1 : IDbProviderFactory
        {
            public IDataBaseConfiguration DefaultDataBaseConfiguration { get; }
            public IDbProvider Create() { return new DbProviderStub( null ); }
            public IDbProvider Create( IDataBaseConfiguration configuration ) { return new DbProviderStub( null ); }
            public DbProviderFactory1( IDataBaseConfiguration config ) { DefaultDataBaseConfiguration = config; } 
        }
        private class DbProviderFactory2 : DbProviderFactory1
        {
            public DbProviderFactory2( IDataBaseConfiguration config ) : base( config ) { }
        }
        private interface ISubscriber1 : IEventSubscriber { }
        private interface ISubscriber2 : IEventSubscriber { }
        private interface ISubscriber3 : ISubscriber2 { }
        private class Subscriber1 : IEventSubscriber
        {
            public void Handle( Message message ){}
            public void Replay( Message message ){}
            public IEnumerable<Type> HandledEventTypes => new List<Type>();
            public event EventHandler<SequenceEventArgs> SequenceFinished;
            public event EventHandler<MessageEventArgs> MessageHandled;
        }
        private class Subscriber2 : Subscriber1 { }
        private class Subscriber3 : EventQueueSubscriber { public Subscriber3( IEventStoreSubscriberContext context ) : base( context ) {} }

        private class Subscriber4 : Subscriber1, ISubscriber1, ISubscriber3{}

        private void InitializeContainer( Action<IEventStoreKitServiceBuilder> initializer = null )
        {
            Builder.InitializeEventStoreKitService( initializer );
            Container = Builder.Build();
            Service = Container.Resolve<IEventStoreKitService>().OfType<EventStoreKitService>();
        }

        #endregion
        
        [Test]
        public void SubscriberRegisteredByContainerShouldBeAvailableThroughTheService()
        {
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            InitializeContainer();

            Service.GetSubscriber<Subscriber1>().Should().Be( Container.Resolve<Subscriber1>() );
        }

        [Test]
        public void SubscriberRegisteredByServiceShouldBeAvailableThroughTheContainer()
        {
            InitializeContainer( builder => builder.RegisterEventSubscriber<Subscriber1>() );

            Container.Resolve<Subscriber1>().Should().Be( Service.GetSubscriber<Subscriber1>() );
        }

        [Test]
        public void SubscribersShouldBeCrossRegisteredInServiceAndContainer()
        {
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            InitializeContainer( builder => builder.RegisterEventSubscriber<Subscriber2>() );

            Container.Resolve<Subscriber1>().Should().Be( Service.GetSubscriber<Subscriber1>() );
            Container.Resolve<Subscriber2>().Should().Be( Service.GetSubscriber<Subscriber2>() );
        }
        
        [Test]
        public void SubscriberShouldBeResolvedWithContext()
        {
            Builder.RegisterType<Subscriber3>().AsSelf().SingleInstance();

            InitializeContainer();

            Service.GetSubscriber<Subscriber3>().Should().Be( Container.Resolve<Subscriber3>() );
        }

        [Test]
        public void SubscriberShouldBeRegisteredInServiceAsAllImplementedSubscriberInterfaces()
        {
            Builder
                .RegisterType<Subscriber4>()
                .As<ISubscriber1>()
                .SingleInstance();
            InitializeContainer();

            var subscriber = Container.Resolve<ISubscriber1>();
            Service.GetSubscriber<Subscriber4>().Should().Be( subscriber );
            Service.GetSubscriber<ISubscriber1>().Should().Be( subscriber );
            Service.GetSubscriber<ISubscriber2>().Should().Be( subscriber );
            Service.GetSubscriber<ISubscriber3>().Should().Be( subscriber );
        }
    }
}
