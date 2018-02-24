using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    public class SubscriberWaitMessageTests
    {
        #region private members

        private class Message1 : Message { public string Id; public string Key; }
        private class Message2 : Message { public string Id; public string Key; }

        private class MessageProcessInfo
        {
            public Message Message;
            public bool IsReplay;
        }
        private class TestSubscriber1 : EventQueueSubscriber
        {
            public readonly List<MessageProcessInfo> ProcessedMessages = new List<MessageProcessInfo>();
            public readonly List<MessageProcessInfo> PreProcessedMessages = new List<MessageProcessInfo>();

            protected override void PreprocessMessage( Message message )
            {
                PreProcessedMessages.Add( new MessageProcessInfo { Message = message } );
                Console.WriteLine( "PreprocessMessage : " + message.GetType().Name + " " + PreProcessedMessages.Count );
            }
            protected override void OnStreamOnIdle( StreamOnIdleEvent message ){}

            private void ProcessTestMessage( Message message )
            {
                ProcessedMessages.Add( new MessageProcessInfo
                {
                    Message = message,
                    IsReplay = IsRebuild
                } );
            }

            public TestSubscriber1( IEventStoreSubscriberContext context ) : base( context )
            {
                Register<Message1>( ProcessTestMessage );
                Register<Message2>( msg =>
                {
                    Thread.Sleep( 50 );
                    ProcessTestMessage( msg );
                } );
            }

            
        }

        private TestSubscriber1 Subscriber1;

        [SetUp]
        protected void Setup()
        {
            var logger = Substitute.For<ILoggerFactory>();
            var scheduler = new NewThreadScheduler();
            Subscriber1 = new TestSubscriber1( new EventStoreSubscriberContext( new EventStoreConfiguration(), logger, scheduler, null ) );
        }

        #endregion

        [Test]
        public void WaitMessagesMethodShouldWaitTillAllQueuedMessagesProcessed()
        {
            const int count = 30;
            for ( var i = 0; i < count; i++ )
                Subscriber1.Handle( new Message1 { Id = i.ToString() } );

            Subscriber1.QueuedMessages().Wait();

            Subscriber1.ProcessedMessages.Count.Should().Be( count );
        }
        
        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessageHandledByType()
        {
            var id1 = "2";

            var task = Subscriber1.When<Message2>( msg => msg.Id == id1 );
            Subscriber1.Handle( new Message1 { Id = "0" } );
            Subscriber1.Handle( new Message2 { Id = "1" } );
            Subscriber1.Handle( new Message2 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            task.Wait();
            task.Result.Id.Should().Be( id1 );
        }

        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessageHandledAsBasic()
        {
            var id1 = "2";

            var task = Subscriber1.When<Message2>( msg => msg.Id == id1 );
            Subscriber1.Handle( new Message1 { Id = "0" } );
            Subscriber1.Handle( new Message2 { Id = "1" } );
            Subscriber1.Handle( new Message2 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            task.Wait();
            task.Result.OfType<Message2>().Id.Should().Be( id1 );
        }

        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessagesHandledUnordered()
        {
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message1>( msg => msg.Id == "2" )
                    .And<Message2>( msg => msg.Id == "3" ) );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            task.Wait();
            task.Result[0].OfType<Message1>().Id.Should().Be( "2" );
            task.Result[1].OfType<Message2>().Id.Should().Be( "3" );
        }

        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessagesHandledUnorderedInReverseOrder()
        {
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message2>( msg => msg.Id == "3" )
                    .And<Message1>( msg => msg.Id == "2" ) );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            task.Wait();
            task.Result[0].OfType<Message1>().Id.Should().Be( "2" );
            task.Result[1].OfType<Message2>().Id.Should().Be( "3" );
        }

        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessagesHandledOrdered()
        {
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message1>( msg => msg.Id == "2" )
                    .And<Message2>( msg => msg.Id == "3" )
                    .Ordered() );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            task.Wait();
            task.Result[0].OfType<Message1>().Id.Should().Be( "2" );
            task.Result[1].OfType<Message2>().Id.Should().Be( "3" );
        }

        [Test]
        public void WhenMethodShouldNotCatchTheMomentWhenMessagesHandledInWrongOrder()
        {
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message2>( msg => msg.Id == "3" )
                    .And<Message1>( msg => msg.Id == "2" )
                    .Ordered() );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            task.Wait( 200 );

            task.IsCompleted.Should().Be( false );
        }

        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessagesHandledUnorderedOrBreakByMessage()
        {
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message1>( msg => msg.Id == "2" )
                    .And<Message2>( msg => msg.Id == "3" )
                    .BreakBy<Message1>( msg => msg.Id == "error" ) );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "error" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            task.Wait();
            task.Result.Count.Should().Be( 1 );
            task.Result[0].OfType<Message1>().Id.Should().Be( "error" );
        }

        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessagesHandledUnorderedOrBreakByException()
        {
            var exception = new Exception( "exception1" );
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message1>( msg => msg.Id == "2" )
                    .And<Message2>( msg => msg.Id == "3" )
                    .BreakBy<Message1>( 
                        msg =>
                        {
                            if ( msg.Id == "error" )
                                throw exception;
                            return false;
                        } ) );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "error" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            new Action( () => task.Wait() ).ShouldThrow<Exception>().WithMessage( exception.Message );
        }


        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessagesHandledOrderedOrBreakByMessage()
        {
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message1>( msg => msg.Id == "2" )
                    .And<Message2>( msg => msg.Id == "3" )
                    .Ordered()
                    .BreakBy<Message1>( msg => msg.Id == "error" ) );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "error" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            task.Wait();
            task.Result.Count.Should().Be( 1 );
            task.Result[0].OfType<Message1>().Id.Should().Be( "error" );
        }

        [Test]
        public void WhenMethodShouldCatchTheMomentWhenMessagesHandledOrderedOrBreakByException()
        {
            var exception = new Exception( "exception1" );
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message1>( msg => msg.Id == "2" )
                    .And<Message2>( msg => msg.Id == "3" )
                    .Ordered()
                    .BreakBy<Message1>(
                        msg =>
                        {
                            if( msg.Id == "error" )
                                throw exception;
                            return false;
                        } ) );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "error" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message2 { Id = "3" } );

            new Action( () => task.Wait() ).ShouldThrow<Exception>().WithMessage( exception.Message );
        }


        [Test]
        public void WhenMethodShouldBeStoppedByTimeout()
        {
            var task = Subscriber1
                .When( MessageMatch
                    .Is<Message1>( msg => msg.Id == "1" )
                    .WithTimeout( 100 ) );

            Thread.Sleep( 110 );
            Subscriber1.Handle( new Message1 { Id = "1" } );

            new Action( () => task.Wait() ).ShouldThrow<TimeoutException>();
        }

    }
}
