using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Projections.MessageHandler;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace EventStoreKit.Tests
{
    public class EventQueueSubscriberTests
    {
        #region private members

        private class Message1 : Message { public string Id; public string Key; }
        private class Message2 : Message { public string Id; public string Key; }
        private class Message3 : Message { public string Id; public string Key; }

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
            public int OnIddleCounter;

            protected override void PreprocessMessage( Message message )
            {
                PreProcessedMessages.Add( new MessageProcessInfo { Message = message } );
            }

            protected override void OnSequenceFinished( SequenceMarkerEvent message )
            {
                SequenceMarkerEvents.Add( message.Identity );
            }

            protected override void OnStreamOnIdle( StreamOnIdleEvent message )
            {
                OnIddleCounter++;
            }

            private void ProcessTestMessage( Message message )
            {
                ProcessedMessages.Add( new MessageProcessInfo
                {
                    Message = message,
                    IsReplay = IsRebuild
                } );
            }

            public TestSubscriber1( ILogger logger, IScheduler scheduler, IEventStoreConfiguration config ) : base( logger, scheduler, config )
            {
                Register<Message1>( ProcessTestMessage );
                Register<Message2>( msg =>
                {
                    Thread.Sleep( 50 );
                    ProcessTestMessage( msg );
                } );
            }

            public void RegisterHandler<TMessage>( Action<TMessage> action, ActionMergeMethod mergeMethod ) where TMessage : Message
            {
                Register( action,  mergeMethod );
            }
        }

        private TestSubscriber1 Subscriber1;

        [SetUp]
        protected void Setup()
        {
            var logger = Substitute.For<ILogger>();
            var scheduler = new NewThreadScheduler();
            Subscriber1 = new TestSubscriber1( logger, scheduler, new EventStoreConfiguration() );
        }

        #endregion

        [Test]
        public void EventQueueSubscriberShouldReturnRegisteredEventHandlers()
        {
            Subscriber1.HandledEventTypes.ShouldBeEquivalentTo( new []
            {
                typeof(SequenceMarkerEvent),
                typeof(StreamOnIdleEvent),
                typeof(Message1),
                typeof(Message2)
            } ); 
        }

        #region Process messages

        [Test]
        public void EventQueueSubscriberShouldProcessMessagesByRegisteredHandlers()
        {
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message2 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "3" } );

            Subscriber1.WaitMessages();

            Subscriber1.ProcessedMessages[0].Message.OfType<Message1>().Id.Should().Be( "1" );
            Subscriber1.ProcessedMessages[1].Message.OfType<Message2>().Id.Should().Be( "2" );
            Subscriber1.ProcessedMessages[2].Message.OfType<Message1>().Id.Should().Be( "3" );
        }

        [Test]
        public void EventQueueSubscriberShouldProcessMessagesInReplayMode()
        {
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Replay( new Message2 { Id = "2" } );
            Subscriber1.Replay( new Message1 { Id = "3" } );
            Subscriber1.Handle( new Message1 { Id = "4" } );

            Subscriber1.WaitMessages();

            Subscriber1.ProcessedMessages[0].IsReplay.Should().Be( false );
            Subscriber1.ProcessedMessages[1].IsReplay.Should().Be( true );
            Subscriber1.ProcessedMessages[2].IsReplay.Should().Be( true );
            Subscriber1.ProcessedMessages[3].IsReplay.Should().Be( false );
        }

        #endregion

        #region Register primary message handlers

        [Test]
        public void EventQueueSubscriberShouldRegisterStaticMessageHandlerAsSingleNotSubstitutional()
        {
            var postProcessed1 = false;
            var postProcessed2 = false;
            Subscriber1.RegisterHandler<Message1>( msg => postProcessed1 = true, ActionMergeMethod.SingleDontReplace );
            Subscriber1.RegisterHandler<Message3>( msg => postProcessed2 = true, ActionMergeMethod.SingleDontReplace );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message3 { Id = "3" } );
            Subscriber1.WaitMessages();

            Subscriber1.ProcessedMessages[0].Message.OfType<Message1>().Id.Should().Be( "1" );
            postProcessed1.Should().BeFalse();
            postProcessed2.Should().BeTrue();
        }

        [Test]
        public void EventQueueSubscriberShouldRegisterStaticMessageHandlerAsSingleSubstitutional()
        {
            var postProcessed1 = false;
            var postProcessed2 = false;
            Subscriber1.RegisterHandler<Message1>( msg => postProcessed1 = true, ActionMergeMethod.SingleReplaceExisting );
            Subscriber1.RegisterHandler<Message3>( msg => postProcessed2 = true, ActionMergeMethod.SingleReplaceExisting );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message3 { Id = "3" } );
            Subscriber1.WaitMessages();

            Subscriber1.ProcessedMessages.Should().BeEmpty();
            postProcessed1.Should().BeTrue();
            postProcessed2.Should().BeTrue();
        }

        [Test]
        public void EventQueueSubscriberShouldRegisterStaticMessageHandlerAsMultipleAfter()
        {
            var postProcessed1 = "";
            var postProcessed2 = "";
            Subscriber1.RegisterHandler<Message1>( msg => postProcessed1 += "1", ActionMergeMethod.MultipleRunAfter );
            Subscriber1.RegisterHandler<Message1>( msg => postProcessed1 += "2", ActionMergeMethod.MultipleRunAfter );
            Subscriber1.RegisterHandler<Message3>( msg => postProcessed2 += "1", ActionMergeMethod.MultipleRunAfter );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message3 { Id = "3" } );
            Subscriber1.WaitMessages();

            Subscriber1.ProcessedMessages[0].Message.OfType<Message1>().Id.Should().Be( "1" );
            postProcessed1.Should().Be( "12" );
            postProcessed2.Should().Be( "1" );
        }

        [Test]
        public void EventQueueSubscriberShouldRegisterStaticMessageHandlerAsMultipleBefore()
        {
            var postProcessed1 = "";
            var postProcessed2 = "";
            Subscriber1.RegisterHandler<Message1>( msg => postProcessed1 += "1", ActionMergeMethod.MultipleRunBefore );
            Subscriber1.RegisterHandler<Message1>( msg => postProcessed1 += "2", ActionMergeMethod.MultipleRunBefore );
            Subscriber1.RegisterHandler<Message3>( msg => postProcessed2 += "1", ActionMergeMethod.MultipleRunBefore );

            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message3 { Id = "3" } );
            Subscriber1.WaitMessages();

            Subscriber1.ProcessedMessages[0].Message.OfType<Message1>().Id.Should().Be( "1" );
            postProcessed1.Should().Be( "21" );
            postProcessed2.Should().Be( "1" );
        }

        #endregion

        #region Override system messges process methods

        [Test]
        public void EventQueueSubscriberShouldAllowToOverridePreProcessMessageProcedure()
        {
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message2 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "3" } );
            Subscriber1.WaitMessages();

            Subscriber1.PreProcessedMessages[0].Message.OfType<Message1>().Id.Should().Be( "1" );
            Subscriber1.PreProcessedMessages[1].Message.OfType<Message2>().Id.Should().Be( "2" );
            Subscriber1.PreProcessedMessages[2].Message.OfType<Message1>().Id.Should().Be( "3" );
        }

        [Test]
        public void EventQueueSubscriberShouldAllowToOverrideSequenceMarkerEventProcedure()
        {
            var id = Guid.NewGuid();
            Subscriber1.WaitMessages();
            Subscriber1.Handle( new SequenceMarkerEvent { Identity = id } );
            Subscriber1.Handle( new Message1 { Id = "3" } );
            Subscriber1.WaitMessages();

            Subscriber1.SequenceMarkerEvents.Count.Should().Be( 3 );
            Subscriber1.SequenceMarkerEvents[1].Should().Be( id );
        }

        [Test]
        public void EventQueueSubscriberShouldAllowToOverrideOnIddleProcedure()
        {
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Thread.Sleep( 1000 ); // default onIddle interval is 500ms

            Subscriber1.OnIddleCounter.Should().Be( 1 );
        }

        [Test]
        public void EventQueueSubscriberShouldRaiseOnIddleEventIfMessageQueueIsEmpty()
        {
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Thread.Sleep( 1200 );

            Subscriber1.Handle( new Message1 { Id = "2" } );
            Thread.Sleep( 100 );
            Subscriber1.Handle( new Message2 { Id = "3" } );
            Subscriber1.Handle( new Message1 { Id = "4" } );
            Thread.Sleep( 1200 );

            Subscriber1.OnIddleCounter.Should().Be( 2 );
        }

        #endregion

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
