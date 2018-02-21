using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Projections.MessageHandler;
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
            public readonly List<Guid> SequenceMarkerEvents = new List<Guid>();

            protected override void PreprocessMessage( Message message )
            {
                PreProcessedMessages.Add( new MessageProcessInfo { Message = message } );
                Console.WriteLine( "PreprocessMessage : " + message.GetType().Name + " " + PreProcessedMessages.Count );
            }
            protected override void OnSequenceFinished( SequenceMarkerEvent message )
            {
                SequenceMarkerEvents.Add( message.Identity );
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

            Subscriber1.WaitMessages1();

            Subscriber1.ProcessedMessages.Count.Should().Be( count );
        }
        [Test]
        public void test1()
        {
            var task = Subscriber1.When<Message1>( msg => msg.Id == "2" );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );

            task.Wait();
            task.Result.Id.Should().Be( "2" );

            //var result = await Subscriber1.Catch();

        }

        [Test]
        public async void test2()
        {
            var task = Subscriber1.When<Message1>( msg => msg.Id == "2" );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );

            task.Wait();
            task.Result.Id.Should().Be( "2" );

            //var result = await Subscriber1.Catch();

        }

        #region CatchEvent functionality tests

        [Test]
        public void EventQueueSubscriberShouldCatchSingleMessageSyncronously()
        {
            new Task( () =>
            {
                Thread.Sleep( 100 );
                Subscriber1.Handle( new Message1 {Id = "1"} );
                Subscriber1.Handle( new Message1 {Id = "2"} );
            } ).Start();

            var catched = Subscriber1.CatchMessage<Message1>( msg => msg.Id == "2" );

            catched.Id.Should().Be( "2" );
        }
        
        [Test]
        public void EventQueueSubscriberShouldCatchBasicSingleMessageSyncronously()
        {
            new Task( () =>
            {
                Thread.Sleep( 100 );
                Subscriber1.Handle( new Message1 { Id = "1" } );
                Subscriber1.Handle( new Message1 { Id = "2" } );
            } ).Start();

            var catched = Subscriber1.CatchMessage<Message>( msg => msg.OfType<Message1>().Id == "2" );

            catched.OfType<Message1>().Id.Should().Be( "2" );
        }

        [Test]
        public void EventQueueSubscriberShouldCatchFirstMatchedSingleMessageSyncronously()
        {
            new Task( () =>
            {
                Thread.Sleep( 100 );
                Subscriber1.Handle( new Message1 { Id = "1" } );
                Subscriber1.Handle( new Message1 { Id = "2" } );
            } ).Start();

            var catched = Subscriber1.CatchMessage<Message1>( msg => msg.Id == "1" || msg.Id == "2" );

            catched.OfType<Message1>().Id.Should().Be( "1" );
        }

        [Test]
        public void EventQueueSubscriberShouldStopCatchingSingleMessageSyncronouslyByTimeout()
        {
            new Task( () =>
            {
                Thread.Sleep( 2000 );
                Subscriber1.Handle( new Message1 { Id = "1" } );
                Subscriber1.Handle( new Message1 { Id = "2" } );
            } ).Start();

            var catched = Subscriber1.CatchMessage<Message1>( msg => msg.Id == "1", 100 );

            catched.Should().BeNull();
        }

        [Test]
        public void EventQueueSubscriberShouldCatchMultipleMessagesSyncronously()
        {
            new Task( () =>
            {
                Thread.Sleep( 100 );
                Subscriber1.Handle( new Message1 { Id = "1" } );
                Subscriber1.Handle( new Message1 { Id = "2" } );
                Subscriber1.Handle( new Message1 { Id = "3" } );
            } ).Start();

            var catched = Subscriber1.CatchMessages<Message1>( msg => msg.Id == "1", msg => msg.Id == "3" );

            catched[0].Id.Should().Be( "1" );
            catched[1].Id.Should().Be( "3" );
        }

        [Test]
        public void EventQueueSubscriberShouldCatchMultipleMessagesAsync()
        {
            var task = Subscriber1.CatchMessagesAsync<Message1>( msg => msg.Id == "1", msg => msg.Id == "3" );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "3" } );

            task.Wait();
            var catched = task.Result;

            catched[0].Id.Should().Be( "1" );
            catched[1].Id.Should().Be( "3" );
        }

        [Test]
        public void EventQueueSubscriberShouldNotCancelCatchingMultipleMessagesAsync()
        {
            var task = Subscriber1.CatchMessagesAsync<Message1>( 
                mandatory: new Func<Message1,bool>[] { msg => msg.Id == "1", msg => msg.Id == "3" },
                optional: new Func<Message1, bool>[] { msg => 
                    {
                        if ( msg.Id == "wrong" )
                            throw new Exception();
                        return false; // if return true - it will not be processed for other messages, because handler already "processed"
                    } } );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "3" } );
            Subscriber1.Handle( new Message1 { Id = "wrong" } );

            task.Wait();
            var catched = task.Result;

            catched[0].Id.Should().Be( "1" );
            catched[1].Id.Should().Be( "3" );
        }

        [Test]
        public void EventQueueSubscriberShouldCatchMultipleMessagesAsyncInAnyOrder()
        {
            var task = Subscriber1.CatchMessagesAsync<Message1>(
                mandatory: new Func<Message1, bool>[] { msg => msg.Id == "1", msg => msg.Id == "3" },
                optional: new Func<Message1, bool>[] { msg =>
                    {
                        if ( msg.Id == "wrong" )
                            throw new Exception();
                        return false; // if return true - it will not be processed for other messages, because handler already "processed"
                    } },
                sequence: false );

            Subscriber1.Handle( new Message1 { Id = "3" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "wrong" } );

            task.Wait();
            var catched = task.Result;

            catched[0].Id.Should().Be( "3" );
            catched[1].Id.Should().Be( "1" );
        }

        [Test]
        public void EventQueueSubscriberShouldCancelCatchingMultipleMessagesAsync()
        {
            var task = Subscriber1.CatchMessagesAsync<Message1>(
                mandatory: new Func<Message1, bool>[] { msg => msg.Id == "1", msg => msg.Id == "3" },
                optional: new Func<Message1, bool>[] { msg =>
                    {
                        if ( msg.Id == "wrong" )
                            throw new InvalidOperationException();
                        return false; // if return true - it will not be processed for other messages, because handler already "processed"
                    } } );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "wrong" } );
            Subscriber1.Handle( new Message1 { Id = "3" } );

            new Action( () => task.Wait( 1000 ) ).ShouldThrow<InvalidOperationException>();
        }

        [Test]
        public void EventQueueSubscriberShouldCatchMultipleMessagesSequenceAsync()
        {
            var task = Subscriber1.CatchMessagesAsync<Message1>(
                mandatory: new Func<Message1, bool>[] { msg => msg.Id == "1", msg => msg.Id == "3" },
                optional: new Func<Message1, bool>[] { msg =>
                    {
                        if ( msg.Id == "wrong" )
                            throw new InvalidOperationException();
                        return false; // if return true - it will not be processed for other messages, because handler already "processed"
                    } },
                sequence: true );

            Subscriber1.Handle( new Message1 { Id = "3", Key = "0" } );
            Subscriber1.Handle( new Message1 { Id = "1", Key = "0" } );
            Subscriber1.Handle( new Message1 { Id = "2", Key = "0" } );
            Subscriber1.Handle( new Message1 { Id = "3", Key = "1" } );
            Subscriber1.Handle( new Message1 { Id = "wrong" } );

            task.Wait();
            var catched = task.Result;
            catched[0].Id.Should().Be( "1" );
            catched[1].Id.Should().Be( "3" );
            catched[1].Key.Should().Be( "1" );
        }

        [Test]
        public void EventQueueSubscriberShouldNotCatchMultipleMessagesSequenceAsyncInWrongOrder()
        {
            var task = Subscriber1.CatchMessagesAsync<Message1>(
                mandatory: new Func<Message1, bool>[] { msg => msg.Id == "1", msg => msg.Id == "3" },
                optional: new Func<Message1, bool>[] { msg =>
                    {
                        if ( msg.Id == "wrong" )
                            throw new InvalidOperationException();
                        return false; // if return true - it will not be processed for other messages, because handler already "processed"
                    } },
                sequence: true );

            Subscriber1.Handle( new Message1 { Id = "3" } );
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "wrong" } );

            new Action( () => task.Wait( 1000 ) ).ShouldThrow<InvalidOperationException>();
        }

        [Test]
        public void EventQueueSubscriberShouldBreakCatchingMultipleMessagesAsyncByTimeout()
        {
            var task = Subscriber1.CatchMessagesAsync<Message1>(
                mandatory: new Func<Message1, bool>[]
                {
                    msg => msg.Id == "1",
                    msg =>
                    {
                        Thread.Sleep( 60 );
                        return msg.Id == "3";
                    }
                },
                optional: new Func<Message1, bool>[] { msg =>
                    {
                        if ( msg.Id == "wrong" )
                            throw new InvalidOperationException();
                        return false; // if return true - it will not be processed for other messages, because handler already "processed"
                    } },
                timeout: 50 );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "3" } );

            new Action( () => task.Wait() ).ShouldThrow<TimeoutException>();
        }

        [Test]
        public void EventQueueSubscriberShouldOnCatchingMultipleMessagesAsyncShouldWaitAllQueuedMessagesProcessing()
        {
            var task = Subscriber1.CatchMessagesAsync<Message1>(
                mandatory: new Func<Message1, bool>[] { msg => msg.Id == "1", msg => msg.Id == "3" },
                optional: new Func<Message1, bool>[] { msg =>
                    {
                        if ( msg.Id == "wrong" )
                            throw new InvalidOperationException();
                        return false; // if return true - it will not be processed for other messages, because handler already "processed"
                    } },
                waitUnprocessed: true );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message1 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "3" } );
            Subscriber1.Handle( new Message1 { Id = "4" } );
            Subscriber1.Handle( new Message1 { Id = "5" } );

            task.Wait();
            task.Status.Should().Be( TaskStatus.RanToCompletion );
            Subscriber1.ProcessedMessages.Count.Should().Be( 5 );
        }

        #endregion

    }
}
