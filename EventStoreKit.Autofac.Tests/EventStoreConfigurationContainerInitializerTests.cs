using Autofac;
using EventStoreKit.Services.Configuration;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreConfigurationContainerInitializerTests : BasicContainerInitializerTests
    {
        [Test]
        public void ConfigurationSetByServiceShouldBeAvailableThroughTheContainer()
        {
            var config = new EventStoreConfiguration();
            InitializeContainer( service => service.SetConfiguration( config ) );
            
            Container.Resolve<IEventStoreConfiguration>().Should().Be( config );
        }

        [Test]
        public void ConfigurationSetByContainerShouldBeAvailableThroughTheService()
        {
            var config = new EventStoreConfiguration();
            Builder.RegisterInstance( config ).As<IEventStoreConfiguration>();
            InitializeContainer();

            Service.Configuration.Value.Should().Be( config );
        }
    }
}
