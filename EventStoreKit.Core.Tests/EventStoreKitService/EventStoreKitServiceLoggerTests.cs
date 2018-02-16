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
        #region Private members

        private EventStoreKitService Service;

        [TearDown]
        protected void Teardown()
        {
            Service?.Dispose();
        }

        #endregion

        [Test]
        public void ServiceShouldInitalizeDefaultLogger()
        {
            Service = new EventStoreKitService();
            var loggerFactory = Service.LoggerFactory.Value;

            loggerFactory.Should().Be( Service.LoggerFactory.Default );
        }

        [Test]
        public void ServiceShouldUseOverridedLogger()
        {
            var overrided = Substitute.For<ILoggerFactory>();

            Service = new EventStoreKitService( false );
            Service.LoggerFactory.Value = overrided;
            Service.Initialize();

            var actual = Service.LoggerFactory.Value;
            actual.Should().Be( overrided );
        }

        [Test]
        public void ServiceShouldUseLoggerOverridedByMethod()
        {
            var overrided = Substitute.For<ILoggerFactory>();

            Service = new EventStoreKitService( false );
            Service
                .SetLoggerFactory( overrided )
                .Initialize();

            var actual = Service.LoggerFactory.Value;
            actual.Should().Be( overrided );
        }
    }
}
