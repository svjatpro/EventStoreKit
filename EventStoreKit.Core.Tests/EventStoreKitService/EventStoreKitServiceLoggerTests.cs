using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using FluentAssertions;
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
            var config = service.Configuration.Value;

            service.Configuration.Value.Should().Be( service.Configuration.Default );
            config.InsertBufferSize.Should().Be( 10000 );
            config.OnIddleInterval.Should().Be( 500 );
        }

        [Test]
        public void ServiceShouldUseOverridedConfiguration()
        {
            const int buffSize = 123;
            const int interval = 234;

            var service = new EventStoreKitService( false );
            service.Configuration.Value =
                new EventStoreConfiguration
                {
                    InsertBufferSize = buffSize,
                    OnIddleInterval = interval
                };
            service.Initialize();

            var config = service.Configuration.Value;

            config.InsertBufferSize.Should().Be( buffSize );
            config.OnIddleInterval.Should().Be( interval );
        }

    }
}
