using EventStoreKit.Logging;
using EventStoreKit.Services;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceLoggerTests
    {
        [Test]
        public void ServiceShouldInitalizeDefaultLogger()
        {
            var service = new EventStoreKitService();
            var loggerFactory = service.LoggerFactory.Value;

            loggerFactory.Should().Be( service.LoggerFactory.Default );
        }

        [Test]
        public void ServiceShouldUseOverridedLogger()
        {
            var overrided = Substitute.For<ILoggerFactory>();

            var service = new EventStoreKitService( false );
            service.LoggerFactory.Value = overrided;
            service.Initialize();

            var actual = service.LoggerFactory.Value;
            actual.Should().Be( overrided );
        }

        [Test]
        public void ServiceShouldUseLoggerOverridedByMethod()
        {
            var overrided = Substitute.For<ILoggerFactory>();

            var service = new EventStoreKitService( false );
            service
                .SetLoggerFactory( overrided )
                .Initialize();

            var actual = service.LoggerFactory.Value;
            actual.Should().Be( overrided );
        }
    }
}
