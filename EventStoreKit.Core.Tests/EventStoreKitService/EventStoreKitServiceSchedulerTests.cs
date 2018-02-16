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
        #region Private members

        private EventStoreKitService Service;

        [TearDown]
        protected void Teardown()
        {
            Service?.Dispose();
        }

        #endregion

        [Test]
        public void ServiceShouldInitalizeDefaultScheduler()
        {
            Service = new EventStoreKitService();
            var scheduler = Service.Scheduler.Value;

            scheduler.Should().Be( Service.Scheduler.Default );
        }

        [Test]
        public void ServiceShouldUseOverridedScheduler()
        {
            var overrided = Substitute.For<IScheduler>();

            Service = new EventStoreKitService( false );
            Service.Scheduler.Value = overrided;
            Service.Initialize();

            var actual = Service.Scheduler.Value;
            actual.Should().Be( overrided );
        }

        [Test]
        public void ServiceShouldUseSchedulerOverridedByMerhod()
        {
            var overrided = Substitute.For<IScheduler>();

            Service = new EventStoreKitService( false );
            Service
                .SetScheduler( overrided )
                .Initialize();

            var actual = Service.Scheduler.Value;
            actual.Should().Be( overrided );
        }
    }
}
