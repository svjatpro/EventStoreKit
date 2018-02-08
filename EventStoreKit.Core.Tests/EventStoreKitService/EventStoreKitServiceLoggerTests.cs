using EventStoreKit.Logging;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
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
            var logger = service.Logger.Value;

            service.Logger.Value.Should().Be( service.Logger.Default );
        }

        [Test]
        public void ServiceShouldUseOverridedLogger()
        {
            var overridedLogger = Substitute.For<ILogger>();

            var service = new EventStoreKitService( false );
            service.Logger.Value = overridedLogger;
            service.Initialize();

            var logger = service.Logger.Value;
            logger.Should().Be( overridedLogger );
        }

    }
}
