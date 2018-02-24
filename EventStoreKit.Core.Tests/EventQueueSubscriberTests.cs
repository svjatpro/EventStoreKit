using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
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
            public int OnIddleCounter;

            protected override void PreprocessMessage( Message message )
            {
                PreProcessedMessages.Add( new MessageProcessInfo { Message = message } );
                Console.WriteLine( "PreprocessMessage : " + message.GetType().Name + " " + PreProcessedMessages.Count );
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

            public TestSubscriber1( IEventStoreSubscriberContext context ) : base( context )
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
            var logger = Substitute.For<ILoggerFactory>();
            var scheduler = new NewThreadScheduler();
            Subscriber1 = new TestSubscriber1( new EventStoreSubscriberContext( new EventStoreConfiguration(), logger, scheduler, null ) );
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

            Subscriber1.QueuedMessages().Wait();

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

            Subscriber1.QueuedMessages().Wait();

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
            Subscriber1.QueuedMessages().Wait();

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
            Subscriber1.QueuedMessages().Wait();

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
            Subscriber1.QueuedMessages().Wait();

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
            Subscriber1.QueuedMessages().Wait();

            Subscriber1.ProcessedMessages[0].Message.OfType<Message1>().Id.Should().Be( "1" );
            postProcessed1.Should().Be( "21" );
            postProcessed2.Should().Be( "1" );
        }

        #endregion

        #region Override system messages process methods

        [Test]
        public void EventQueueSubscriberShouldAllowToOverridePreProcessMessageProcedure()
        {
            Subscriber1.Handle( new Message1 { Id = "1" } );
            Subscriber1.Handle( new Message2 { Id = "2" } );
            Subscriber1.Handle( new Message1 { Id = "3" } );
            Subscriber1.QueuedMessages().Wait();

            var start = 0;
            for ( ; start < Subscriber1.PreProcessedMessages.Count && Subscriber1.PreProcessedMessages[start].Message is StreamOnIdleEvent; start++ );

            Subscriber1.PreProcessedMessages[start++].Message.OfType<Message1>().Id.Should().Be( "1" );
            Subscriber1.PreProcessedMessages[start++].Message.OfType<Message2>().Id.Should().Be( "2" );
            Subscriber1.PreProcessedMessages[start].Message.OfType<Message1>().Id.Should().Be( "3" );
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
            Thread.Sleep( 1300 );

            Subscriber1.OnIddleCounter.Should().BeGreaterOrEqualTo( 1 );
        }

        #endregion

        #region MessageHandledEvent

        [Test]
        public void SubscriberShould()
        {
            var processed = new List<Message>();
            Subscriber1.MessageHandled += ( o, arg ) =>
            {
                if( !(arg.Message is StreamOnIdleEvent ) )
                    processed.Add( arg.Message );
            };

            Subscriber1.Handle( new Message1{ Id = "1" } );
            Subscriber1.Handle( new Message2{ Id = "2" } );

            Thread.Sleep( 200 ); // can't use wait/catch here because it is planned to move out of the EventSubscriber

            processed[0].OfType<Message1>().Id.Should().Be( "1" );
            processed[1].OfType<Message2>().Id.Should().Be( "2" );
        }

        #endregion
    }
}
