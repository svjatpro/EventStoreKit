using System.Reactive.Concurrency;
using Autofac;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreSchedulerContainerInitializerTests : BasicContainerInitializerTests
    {
        [Test]
        public void SchedulerSetByServiceShouldBeAvailableThroughTheContainer()
        {
            var scheduler = Substitute.For<IScheduler>();
            InitializeContainer( service => service.SetScheduler( scheduler ) );
            
            Container.Resolve<IScheduler>().Should().Be( scheduler );
        }

        [Test]
        public void SchedulerSetByContainerShouldBeAvailableThroughTheService()
        {
            var scheduler = Substitute.For<IScheduler>();
            Builder.RegisterInstance( scheduler ).As<IScheduler>();
            InitializeContainer();

            Service.Scheduler.Value.Should().Be( scheduler );
        }
    }
}
