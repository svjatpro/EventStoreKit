using System;
using EventStoreKit.Messages;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Core.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class CommandSenderTests
    {
        [Test]
        public void CommandSenderShouldDispatchTheCommand()
        {
            var message = Substitute.For<Message>();
            var dispatcher = Substitute.For<IMessageDispatcher<Message>>();
            var publisher = new CommandSender<Message>( dispatcher );

            publisher.SendCommand( message );

            dispatcher.Received().Dispatch( message );
        }

        [Test]
        public void CommandSenderShouldGetDispatcherThroughTheConstructor()
        {
            new Action( () => new CommandSender<Message>(null) )
                .Should().Throw<ArgumentNullException>();
        }

    }
}
