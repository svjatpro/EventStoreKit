using System;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

//[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace EventStoreKit.Core.Tests.MessageDispatcher
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class MessageDispatcherTests
    {
        private class Event1 : Message{}
        private class Event2 : Message{}
        private class Event3 {}

        [Test]
        public void DispatcherShouldDispatchMessages()
        {
            var count1 = 0;
            var count2 = 0;
            var dispatcher = new MessageDispatcher<Message>( Substitute.For<ILogger<MessageDispatcher<Message>>>() );
            
            dispatcher.RegisterHandler<Event1>( msg => { count1++; } );
            dispatcher.RegisterHandler<Event2>( msg => { count2++; } );
            
            dispatcher.Dispatch( new Event1() );
            dispatcher.Dispatch( new Event2() );
            dispatcher.Dispatch( new Event1() );
            dispatcher.Dispatch( new Event2() );
            dispatcher.Dispatch( new Event2() );

            count1.Should().Be( 2 );
            count2.Should().Be( 3 );
        }

        [Test]
        public void DispatcherShouldDispatchMultipleRoutesForSingleMessageByDefault()
        {
            var count1 = 0;
            var count2 = 0;
            var dispatcher = new MessageDispatcher<Message>(Substitute.For<ILogger<MessageDispatcher<Message>>>());

            dispatcher.RegisterHandler<Event1>(msg => { count1++; });
            dispatcher.RegisterHandler<Event1>(msg => { count2++; });
            dispatcher.RegisterHandler<Event2>(msg => { count2++; });

            dispatcher.Dispatch(new Event1());
            dispatcher.Dispatch(new Event2());
            dispatcher.Dispatch(new Event1());

            count1.Should().Be(2);
            count2.Should().Be(3);
        }

        [Test]
        public void DispatcherShouldDispatchNotAllowMultipleRoutesForSingleMessage()
        {
            var dispatcher = new MessageDispatcher<Message>(Substitute.For<ILogger<MessageDispatcher<Message>>>());

            dispatcher.RegisterHandler<Event1>( msg => {}, false );
            Assert.Throws<InvalidOperationException>( () => dispatcher.RegisterHandler<Event1>( msg => {}, false ) );
        }

        [Test]
        public void DispatcherShouldIgnoreMessageOfWrongType()
        {
            var dispatcher = new MessageDispatcher<Message>(Substitute.For<ILogger<MessageDispatcher<Message>>>());
            dispatcher.Dispatch( (object)new Event3() );
        }
    }
}
