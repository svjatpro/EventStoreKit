using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using Newtonsoft.Json;

namespace EventStoreKit.Projections
{
    public abstract class EventQueueSubscriber : IEventSubscriber
    {
        #region Private fields

        protected readonly ILogger Log;
        private readonly BlockingCollection<EventInfo> MessageQueue;
        private readonly IDictionary<Type, Action<Message>> Actions;
        protected bool IsRebuild;

// ReSharper disable FieldCanBeMadeReadOnly.Local
        private int OnIddleInterval = 500;
// ReSharper restore FieldCanBeMadeReadOnly.Local
        private volatile bool MessageProcessed;
        private System.Threading.Timer OnIddleTimer;
        private readonly object IddleLockObj = new object();
        
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
                    DelegateAdjuster.CastArgument<Message, TMessage>( message => handler.Handle( message ) ) ) );
        }
        
        private void ProcessMessages( EventInfo i )
        {
            var e = i.Event;
            e.CheckNull( "e" );
            IsRebuild = i.IsRebuild;
            try
            {
                //Log.SetAttribute( "Event", JsonConvert.SerializeObject( i.Event ) );
                //LogicalThreadContext.Properties["Event"] = JsonConvert.SerializeObject( i.Event );
                var action = Actions.Where( a => a.Key == e.GetType() ).Select( a => a.Value ).SingleOrDefault();
                if ( action != null )
                {
                    PreprocessMessage( e );
                    action( e );
                    Log.Info( "{0} handled ( version = {1} ). Unprocessed events: {2}", e.GetType().Name, e.Version, MessageQueue.Count );
                }
            }
            catch ( Exception ex )
            {
                Log.Error( 
                    string.Format( "Error occured during processing '{0}' in '{1}': '{2}'", e.GetType().Name, GetType().Name, ex.Message ),
                    ex, new Dictionary<string, string> { { "Event", JsonConvert.SerializeObject( i.Event ) } });
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
                    OnIddleTimer = new Timer( OnIddleTimerHandler, null, OnIddleInterval, OnIddleInterval );
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

        protected void Register<TEvent>( Action<TEvent> action ) where TEvent : Message { Register( action, false ); }
        protected void Register<TEvent>( Action<TEvent> action, bool allowMultiple ) where TEvent : Message
        {
            Register( typeof( TEvent ), DelegateAdjuster.CastArgument<Message, TEvent>( x => action( x ) ), allowMultiple );
        }

        protected void Register( Type eventType, Action<Message> action ) { Register( eventType, action, false ); }
        protected void Register( Type eventType, Action<Message> action, bool allowMultiple )
        {
            if ( Actions.ContainsKey( eventType ) )
            {
                if( allowMultiple )
                {
                    var existingAction = Actions[eventType];
                    var newAction = action;
                    action =
                        e =>
                        {
                            existingAction( e );
                            newAction( e );
                        };
                }
                Actions[eventType] = action;
            }
            else
            {
                Actions.Add( eventType, action );
            }
        }

        protected virtual void PreprocessMessage( Message message ){}
        
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

        protected virtual void OnSequenceFinished( SequenceMarkerEvent message ) { }
        protected virtual void OnStreamOnIdle( StreamOnIdleEvent message ) { }
        
        protected EventQueueSubscriber( ILogger logger, IScheduler scheduler )
        {
            Log = logger.CheckNull( "logger" );

            Actions = new Dictionary<Type, Action<Message>>();
            MessageQueue = new BlockingCollection<EventInfo>();
            MessageQueue.GetConsumingEnumerable()
                .ToObservable( scheduler )
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

            int.TryParse( ConfigurationManager.AppSettings["OnIddleInterval"], out OnIddleInterval );
            
            Register<SequenceMarkerEvent>( Apply, true );
            Register<StreamOnIdleEvent>( Apply, true );

            InitOnIddleTimer();
        }
        
        public void Handle( Message e ) { Handle( e, false ); }
        protected void Handle( Message e, bool isRebuild )
        {
            var eventType = e.GetType();
            Action<Message> action;
            if ( !Actions.TryGetValue( eventType, out action ) )
                return;
            MessageQueue.Add( new EventInfo { Event = e, IsRebuild = isRebuild } );
            Log.Debug( "Unprocessed events: {0}", MessageQueue.Count );
        }

        public virtual void Replay( Message e )
        {
            Handle( e, true );
        }

        public IEnumerable<Type> HandledEventTypes
        {
            get { return Actions == null ? new Type[0] : Actions.Keys; }
        }
    }
}
