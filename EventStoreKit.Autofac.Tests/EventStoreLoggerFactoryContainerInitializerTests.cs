using Autofac;
using EventStoreKit.Logging;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreLoggerFactoryContainerInitializerTests : BasicContainerInitializerTests
    {
        [Test]
        public void LoggerFactorySetByServiceShouldBeAvailableThroughTheContainer()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            InitializeContainer( service => service.SetLoggerFactory( loggerFactory ) );
            
            Container.Resolve<ILoggerFactory>().Should().Be( loggerFactory );
        }

        [Test]
        public void LoggerFactorySetByContainerShouldBeAvailableThroughTheService()
        {
            var loggerFactory = Substitute.For<ILoggerFactory>();
            Builder.RegisterInstance( loggerFactory ).As<ILoggerFactory>();
            InitializeContainer();

            Service.LoggerFactory.Value.Should().Be( loggerFactory );
        }
    }
}
