using System;
using EventStoreKit.Messages;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Core.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class EventPublisherTests
    {
        [Test]
        public void PublisherShouldDispatchTheEvent()
        {
            var message = Substitute.For<Message>();
            var dispatcher = Substitute.For<IMessageDispatcher<Message>>();
            var publisher = new EventPublisher<Message>( dispatcher );

            publisher.Publish( message );

            dispatcher.Received().Dispatch( message );
        }

        [Test]
        public void PublisherShouldGetDispatcherThroughTheConstructor()
        {
            new Action( () => new EventPublisher<Message>(null) )
                .Should().Throw<ArgumentNullException>();
        }

    }
}
