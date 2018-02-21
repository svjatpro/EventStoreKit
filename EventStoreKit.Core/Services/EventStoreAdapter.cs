using System;
using System.Linq;
using EventStoreKit.Constants;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Utility;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Persistence;

namespace EventStoreKit.Services
{
    public class EventStoreAdapter : IStoreEvents
    {
        private readonly ILogger<EventStoreAdapter> Logger;

        #region Private fields

        private IStoreEvents InternalStore;
        private readonly IEventPublisher EventPublisher;
        private readonly object LockObject = new object();

        private readonly ICommandBus CommandBus;
        
        #endregion

        #region Implementation of IDisposable

        public void Dispose( )
        {
            if ( InternalStore != null )
            {
                InternalStore.Dispose();
                InternalStore = null;
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
                            Logger.Error( "Error dispatching command: {0}", ex.Message );
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
                            @event.ProcessEvent( e =>
                            {
                                e.Version = commit.StreamRevision;
                                e.BucketId = commit.BucketId;
                                e.CheckpointToken = commit.CheckpointToken;

                                EventPublisher.Publish( e );
                            } );
                        }
                    }
                    catch ( Exception ex )
                    {
                        Logger.Error( "Error dispatching event: {0}", ex.Message );
                    }
                }
            }
        }
    }
}
