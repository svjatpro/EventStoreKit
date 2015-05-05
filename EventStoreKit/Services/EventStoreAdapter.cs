using System;
using System.Linq;
using System.Monads;
using EventStoreKit.CommandBus;
using EventStoreKit.Constants;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Utility;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Persistence;

namespace EventStoreKit.Services
{
    public class EventStoreAdapter : IStoreEvents //, IReplaysHistory
    {
        private readonly ILogger<EventStoreAdapter> Logger;

        #region Private fields

        public const string IssuedByHeader = "IssuedBy";
        public const string TimestampHeader = "Timestamp";

        private readonly IStoreEvents InternalStore;
        private readonly IEventPublisher EventPublisher;
        private readonly object LockObject = new object();

        //private IStoreEvents Store { get { return InternalStore; } }
        private readonly ICommandBus CommandBus;

        
        #endregion

        #region Private methods
        
        //private void ProcessEvent( EventMessage evt, Action<Message> eventProccecingMethod )
        //{
        //    if ( evt.Body is IEnumerable<Message> )
        //    {
        //        foreach ( var item in evt.Body as IEnumerable<Message> )
        //            eventProccecingMethod( item );
        //    }
        //    else
        //    {
        //        eventProccecingMethod( evt.Body as Message );
        //    }
        //}

        private void SetTimestamp( Message e, ICommit commit )
        {
            var header = commit.Headers
                .Where( h => h.Key == TimestampHeader )
                .Select( h => h.Value )
                .FirstOrDefault();
            if ( header != null )
                e.Created = (DateTime)header;
        }
        
        #endregion
        
        #region Implementation of IDisposable

        public void Dispose( )
        {
            if ( InternalStore != null )
            {
                InternalStore.Dispose();
            }
        }

        #endregion

        #region Implementation of IStoreEvents

        public IEventStream CreateStream( string bucketId, string streamId )
        {
            return InternalStore.CreateStream( bucketId, streamId );
        }

        public IEventStream OpenStream( string bucketId, string streamId, int minRevision, int maxRevision )
        {
            return InternalStore.OpenStream( bucketId, streamId, minRevision, maxRevision );
        }

        public IEventStream OpenStream( ISnapshot snapshot, int maxRevision )
        {
            return InternalStore.OpenStream( snapshot, maxRevision );
        }

        public void StartDispatchScheduler()
        {
            InternalStore.StartDispatchScheduler();
        }

        public IPersistStreams Advanced
        {
            get { return InternalStore.Advanced; }
        }

        #endregion

        public EventStoreAdapter( 
            Wireup wireup, 
            ILogger<EventStoreAdapter> logger,
            IEventPublisher eventPublisher,
            ICommandBus commandBus )
        {
            CommandBus = commandBus;
            Logger = logger.CheckNull( "logger" );
            EventPublisher = eventPublisher.CheckNull( "eventPublisher" );

            InternalStore = 
                new AsynchronousDispatchSchedulerWireup( 
                    wireup.CheckNull( "wireup" ), 
                    new DelegateMessageDispatcher( DispatchCommit ), 
                    DispatcherSchedulerStartup.Auto )
                .UsingEventUpconversion()
                .WithConvertersFrom( AppDomain.CurrentDomain.GetAssemblies() /*.Where( a => a.FullName.StartsWith( "Code.CL.Domain" ) )*/.ToArray() )
                .Build();
        }

        public void DispatchCommit( ICommit commit )
        {
            lock ( LockObject ) // todo: do we need of the lock? - yes we do!
            {
                if ( commit.Headers.Keys.Any( s => s == EventStoreConstants.SagaType ) )
                {
                    var commands = commit
                        .Headers
                        .Where( x => x.Key.StartsWith( EventStoreConstants.UndispatchedMessage ) )
                        .Select( x => x.Value )
                        .OfType<DomainCommand>()
                        .ToList();
                    foreach ( var cmd in commands )
                    {
                        try
                        {
                            CommandBus.Send( cmd );
                        }
                        catch ( Exception ex )
                        {
                            Logger.ErrorFormat( "Error dispatching command: {0}", ex.Message );
                            EventPublisher.Publish( new CommandDispatchFailedEvent( cmd.Id ) ); // todo: define what exactly should be sent whithin the event
                        }
                    }
                }
                else
                {
                    try
                    {
                        foreach ( var @event in commit.Events )
                        {
                            //ProcessEvent( @event, e =>
                            @event.ProcessEvent( e =>
                            {
                                e.Version = commit.StreamRevision;
                                SetTimestamp( e, commit );
                                EventPublisher.Publish( e );
                            } );
                        }
                    }
                    catch ( Exception ex )
                    {
                        Logger.ErrorFormat( "Error dispatching event: {0}", ex.Message );
                    }
                }
            }
        }
    }
}
