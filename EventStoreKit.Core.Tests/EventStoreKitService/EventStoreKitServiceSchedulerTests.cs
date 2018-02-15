using System.Reactive.Concurrency;
using EventStoreKit.Services;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    [TestFixture]
    public class EventStoreKitServiceSchedulerTests
    {
        [Test]
        public void ServiceShouldInitalizeDefaultScheduler()
        {
            var service = new EventStoreKitService();
            var scheduler = service.Scheduler.Value;

            scheduler.Should().Be( service.Scheduler.Default );
        }

        [Test]
        public void ServiceShouldUseOverridedScheduler()
        {
            var overrided = Substitute.For<IScheduler>();

            var service = new EventStoreKitService( false );
            service.Scheduler.Value = overrided;
            service.Initialize();

            var actual = service.Scheduler.Value;
            actual.Should().Be( overrided );
        }

        [Test]
        public void ServiceShouldUseSchedulerOverridedByMerhod()
        {
            var overrided = Substitute.For<IScheduler>();

            var service = new EventStoreKitService( false );
            service
                .SetScheduler( overrided )
                .Initialize();

            var actual = service.Scheduler.Value;
            actual.Should().Be( overrided );
        }
    }
}
