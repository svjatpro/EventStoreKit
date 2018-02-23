using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections.MessageHandler;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;
using Newtonsoft.Json;

namespace EventStoreKit.Projections
{
    public abstract class EventQueueSubscriber : IEventSubscriber
    {
        #region Private fields

        public const int DefaultWaitMessageTimeout = 10000;

        private readonly BlockingCollection<EventInfo> MessageQueue;
        private readonly Dictionary<Type, IMessageHandler> Handlers;
        
// ReSharper disable RedundantNameQualifier
        private System.Threading.Timer OnIddleTimer;
// ReSharper restore RedundantNameQualifier
        private readonly object IddleLockObj = new object();
        private volatile bool MessageProcessed;

        private readonly IEventStoreConfiguration EventStoreConfig;
        private readonly ILoggerFactory LogFactory;

        #endregion

        #region protected fields

        protected bool IsRebuild;
        protected ILogger Log => LogFactory.Create( GetType() );

        #endregion

        #region internall classes

        public class EventInfo
        {
            public Message Event;
            public bool IsRebuild;
        }

        #endregion

        #region Private methods

// ReSharper disable UnusedMember.Local
        private void RegisterHandler<TMessage>() where TMessage : Message
// ReSharper restore UnusedMember.Local
        {
            this.OfType<IEventHandler<TMessage>>()
                .Do( handler => Register( 
                    typeof( TMessage ), 
                    DelegateAdjuster.CastArgument<Message, TMessage>( handler.Handle ) ) );
        }
        
        private void ProcessMessages( EventInfo i )
        {
            var message = i.Event;
            message.CheckNull( "message" );
            var msgType = message.GetType();
            IsRebuild = i.IsRebuild;
            try
            {
                // process static handlers
                if ( Handlers.ContainsKey( msgType ) )
                {
                    var handler = Handlers[msgType];
                    PreprocessMessage( message );
                    handler.Process( message );
                    Log.Info( "{0} handled ( version = {1} ). Unprocessed events: {2}", msgType.Name, message.Version, MessageQueue.Count );
                }

                if( !IsRebuild )
                    MessageHandled.ExecuteAsync( this, new MessageEventArgs( message ) );
            }
            catch ( Exception ex )
            {
                Log.Error( 
                    string.Format( "Error occured during processing '{0}' in '{1}': '{2}'", msgType.Name, GetType().Name, ex.Message ),
                    ex, new Dictionary<string, string> 
                    { 
                        { "Event", JsonConvert.SerializeObject( message ) }
                        //{ "User", message.CreatedBy }
                    });
            }
            finally
            {
                IsRebuild = false;
            }

            if( !(i.Event is StreamOnIdleEvent) )
            {
                MessageProcessed = true;
                InitOnIddleTimer();
            }
        }

        private void InitOnIddleTimer()
        {
            if ( OnIddleTimer == null )
            {
                lock ( IddleLockObj )
                {
                    OnIddleTimer = new Timer( OnIddleTimerHandler, null, EventStoreConfig.OnIddleInterval, EventStoreConfig.OnIddleInterval );
                }
            }
        }
        private void OnIddleTimerHandler( object state )
        {
            if ( !MessageProcessed )
            {
                lock ( IddleLockObj )
                {
                    if ( OnIddleTimer != null )
                    {
                        OnIddleTimer.Change( Timeout.Infinite, Timeout.Infinite );
                        OnIddleTimer = null;
                    }
                }
                Handle( new StreamOnIdleEvent() );
            }
            else
            {
                MessageProcessed = false;
            }
        }
        
        #endregion

        #region Protected methods

        protected void Register<TEvent>( Action<TEvent> action ) where TEvent : Message { Register( action, ActionMergeMethod.SingleDontReplace ); }
        protected void Register<TEvent>( Action<TEvent> action, ActionMergeMethod mergeMethod ) where TEvent : Message
        {
            Register( typeof( TEvent ), DelegateAdjuster.CastArgument<Message, TEvent>( action ), mergeMethod );
        }

        protected void Register( Type eventType, Action<Message> action ) { Register( eventType, action, ActionMergeMethod.SingleDontReplace ); }
        protected void Register( Type eventType, Action<Message> action, ActionMergeMethod mergeMethod )
        {
            if ( !Handlers.ContainsKey( eventType ) )
            {
                Handlers.Add( eventType, new DirectMessageHandler<Message>( action ) );
                return;
            }

            // merge with existing action
            var existingAction = Handlers[eventType];
            switch ( mergeMethod )
            {
                case ActionMergeMethod.SingleReplaceExisting:
                    Handlers[eventType] = new DirectMessageHandler<Message>( action );
                    break;
                case ActionMergeMethod.MultipleRunAfter:
                    Handlers[eventType] = existingAction.Combine( action );
                    break;
                case ActionMergeMethod.MultipleRunBefore:
                    Handlers[eventType] = existingAction.Combine( action, true );
                    break;
            }
        }

        protected void Handle( Message e, bool isRebuild )
        {
            var eventType = e.GetType();
            if( !Handlers.ContainsKey( eventType ) )
                return;
            MessageQueue.Add( new EventInfo { Event = e, IsRebuild = isRebuild } );
            Log.Debug( "Unprocessed events: {0}", MessageQueue.Count );
        }
        
        /// <summary>
        /// Executes .net events in secure way: 
        ///  - check if there is any subscribers
        ///  - prevent execution during rebuild
        ///  - prevent execution for bulk messages
        /// </summary>
        /// <param name="event">Event handler</param>
        /// <param name="sender">Sender object</param>
        /// <param name="args">Generic event argument</param>
        /// <param name="message">Initial message</param>
        protected void Execute<TArgs>( EventHandler<TArgs> @event, object sender, TArgs args, Message message = null ) where TArgs : EventArgs
        {
            if ( @event != null && !IsRebuild && ( message == null || !message.IsBulk ) )
                @event.BeginInvoke( sender, args, result =>
                {
                    try { ( (EventHandler<TArgs>)( (AsyncResult)result ).AsyncDelegate ).EndInvoke( result ); }
                    catch ( Exception ex )
                    {
                        Log.Error( string.Format( "Error occured during processing '{0}' in '{1}': '{2}'", @event.GetType().Name, GetType().Name, ex.Message ), ex );
                    }
                }, null );
        }
        protected void Execute( EventHandler @event, object sender, EventArgs args, Message message = null )
        {
            if ( @event != null && !IsRebuild && ( message == null || !message.IsBulk ) )
            {
                @event.BeginInvoke( sender, args, result =>
                {
                    try { ( (EventHandler)( (AsyncResult)result ).AsyncDelegate ).EndInvoke( result ); }
                    catch ( Exception ex )
                    {
                        Log.Error( string.Format( "Error occured during processing '{0}' in '{1}': '{2}'", @event.GetType().Name, GetType().Name, ex.Message ), ex );
                    }
                }, null );
            }
        }

        protected virtual void PreprocessMessage( Message message ) { }
        protected virtual void OnSequenceFinished( SequenceMarkerEvent message ) { }
        protected virtual void OnStreamOnIdle( StreamOnIdleEvent message ) { }

        #endregion

        #region Private event handlers

        private void Apply( SequenceMarkerEvent msg )
        {
            OnSequenceFinished( msg );
            SequenceFinished.Execute( this, new SequenceEventArgs( msg.Identity ) );
        }

        private void Apply( StreamOnIdleEvent msg )
        {
            OnStreamOnIdle( msg );
        }

        #endregion

        public event EventHandler<SequenceEventArgs> SequenceFinished;
        public event EventHandler<MessageEventArgs> MessageHandled;

        protected EventQueueSubscriber( IEventStoreSubscriberContext context )
        {
            EventStoreConfig = context.Configuration;
            LogFactory = context.LoggerFactory;

            Handlers = new Dictionary<Type, IMessageHandler>();

            MessageQueue = new BlockingCollection<EventInfo>();
            MessageQueue.GetConsumingEnumerable()
                .ToObservable( context.Scheduler )
                .Subscribe( ProcessMessages );

            var eventHandlerInterfaceType = typeof( IEventHandler<> );
            var eventHanlerInterfaces = GetType()
                .GetInterfaces()
                .Where( i => i.Name == eventHandlerInterfaceType.Name )
                .ToList();
            var createHandlerMehod = typeof( EventQueueSubscriber ).GetMethod( "RegisterHandler", BindingFlags.NonPublic | BindingFlags.Instance );
            eventHanlerInterfaces
                .ForEach( interfaceType =>
                {
                    var messageType = interfaceType.GetGenericArguments()[0];
                    createHandlerMehod.MakeGenericMethod( messageType ).Invoke( this, new object[] { } );
                } );

            Register<SequenceMarkerEvent>( Apply );
            Register<StreamOnIdleEvent>( Apply );

            InitOnIddleTimer();
        }
        
        public void Handle( Message e ) { Handle( e, false ); }
        
        public virtual void Replay( Message message )
        {
            Handle( message, true );
        }

        public IEnumerable<Type> HandledEventTypes
        {
            get { return Handlers.Keys; }
        }
        
    }
}
