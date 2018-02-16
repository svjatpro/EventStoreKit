using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceConfigurationTests
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
        public void ServiceShouldInitalizeDefaultConfiguration()
        {
            Service = new EventStoreKitService();
            var config = Service.Configuration.Value;

            Service.Configuration.Value.Should().Be( Service.Configuration.Default );
            config.InsertBufferSize.Should().Be( 10000 );
            config.OnIddleInterval.Should().Be( 500 );
        }

        [Test]
        public void ServiceShouldUseOverridedConfiguration()
        {
            const int buffSize = 123;
            const int interval = 234;

            Service = new EventStoreKitService( false );
            Service.Configuration.Value =
                new EventStoreConfiguration
                {
                    InsertBufferSize = buffSize,
                    OnIddleInterval = interval
                };
            Service.Initialize();

            var config = Service.Configuration.Value;

            config.InsertBufferSize.Should().Be( buffSize );
            config.OnIddleInterval.Should().Be( interval );
        }

    }
}
