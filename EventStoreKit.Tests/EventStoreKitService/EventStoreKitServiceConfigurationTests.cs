using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceConfigurationTests
    {
        [Test]
        public void ServiceShouldInitalizeDefaultConfiguration()
        {
            var service = new EventStoreKitService();
            var config = service.GetConfiguration();

            config.InsertBufferSize.Should().Be( 10000 );
            config.OnIddleInterval.Should().Be( 500 );
        }

        [Test]
        public void ServiceShouldUseOverridedConfiguration()
        {
            const int buffSize = 123;
            const int interval = 234;

            var service = new EventStoreKitService();
            service.SetConfiguration( new EventStoreConfiguration
            {
                InsertBufferSize = buffSize,
                OnIddleInterval = interval
            } );

            var config = service.GetConfiguration();

            config.InsertBufferSize.Should().Be( buffSize );
            config.OnIddleInterval.Should().Be( interval );
        }

    }
}
