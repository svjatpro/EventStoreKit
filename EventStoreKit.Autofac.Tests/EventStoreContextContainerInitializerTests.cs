using System.Reactive.Concurrency;
using Autofac;
using EventStoreKit.Logging;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreContextContainerInitializerTests : BasicContainerInitializerTests
    {
        #region Private members

        private class Subscriber1 : EventQueueSubscriber
        {
            public readonly IEventStoreSubscriberContext Context;

            public Subscriber1( IEventStoreSubscriberContext context ) :
                base( context )
            {
                Context = context;
            }
        }

        #endregion

        #region Configuration

        [Test]
        public void ConfigurationSetByContextShouldBeProvidedToSubscriberThroughTheContext()
        {
            var config = new EventStoreConfiguration();
            Builder.RegisterInstance( config ).As<IEventStoreConfiguration>();
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            InitializeContainer();

            Service.GetSubscriber<Subscriber1>().Context.Configuration.Should().Be( config );
        }

        [Test]
        public void ConfigurationSetByServiceShouldBeProvidedToSubscriberThroughTheContext()
        {
            var config = new EventStoreConfiguration();
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            InitializeContainer( service => service.SetConfiguration( config ) );

            Service.GetSubscriber<Subscriber1>().Context.Configuration.Should().Be( config );
        }

        #endregion

        #region Configuration

        [Test]
        public void SchedulerSetByContextShouldBeProvidedToSubscriberThroughTheContext()
        {
            var scheduler = Substitute.For<IScheduler>();
            Builder.RegisterInstance( scheduler ).As<IScheduler>();
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            InitializeContainer();

            Service.GetSubscriber<Subscriber1>().Context.Scheduler.Should().Be( scheduler );
        }

        [Test]
        public void SchedulerSetByServiceShouldBeProvidedToSubscriberThroughTheContext()
        {
            var scheduler = Substitute.For<IScheduler>();
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            InitializeContainer( service => service.SetScheduler( scheduler ) );

            Service.GetSubscriber<Subscriber1>().Context.Scheduler.Should().Be( scheduler );
        }

        #endregion

        [Test]
        public void LoggerSetByContextShouldBeProvidedToSubscriberThroughTheContext()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            InitializeContainer( service => service.SetLoggerFactory( loggerFactory ) );
            
            Container.Resolve<IEventStoreConfiguration>().Should().Be( config );
        }

        [Test]
        public void ConfigurationSetByContainerShouldBeAvailableThroughTheService()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            Builder.RegisterType<Subscriber1>().AsSelf().SingleInstance();
            Builder.RegisterInstance( config ).As<IEventStoreConfiguration>();
            InitializeContainer();

            Service.Configuration.Value.Should().Be( config );
        }

        // todo: add test with cross register scenarios: subscriber / config
    }
}
