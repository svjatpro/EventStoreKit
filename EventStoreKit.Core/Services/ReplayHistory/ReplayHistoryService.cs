using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services.ReplayHistory;
using EventStoreKit.Utility;
using NEventStore;

namespace EventStoreKit.Services
{
    public class ReplayHistoryService : IReplaysHistory
    {
        #region Private fields

        private const string SagaTypeHeader = "SagaType";
        private readonly IEventPublisher EventPublisher;
        private readonly ILogger<ReplayHistoryService> Logger;
        private ICommitsIterator Iterator;

        private readonly object LockRebuild = new object();
        private Dictionary<IEventSubscriber, ProjectionRebuildInfo> Subscribers;
        private enum RebuildStatus { Ready, Started, WaitingForProjections }
        private volatile RebuildStatus Status = RebuildStatus.Ready;

        #endregion

        #region Private methods

        private void ReplayCommit( ICommit commit, IEventSubscriber model )
        {
            foreach ( var @event in commit.Events )
            {
                @event.ProcessEvent( e =>
                {
                    e.Version = commit.StreamRevision;
                    model.Replay( e );
                } );
            }
        }

        private void RebuildInternal( 
            List<IEventSubscriber> projections,
            ICommitsIterator iterator, 
            Action<IEventSubscriber, ProjectionRebuildInfo> projectionProgressAction )
        {
            foreach ( var model in projections )
                model.Handle( new SystemCleanedUpEvent() );

            iterator.Reset();
            var commits = iterator.LoadNext();
            while ( commits.Any() )
            {
                foreach ( var commit in commits )
                {
                    if ( commit.Headers.Keys.Any( s => s == SagaTypeHeader ) )
                        continue;
                    foreach ( var model in projections )
                    {
                        ReplayCommit( commit, model );
                    }
                }
                commits = iterator.LoadNext();
                var count = commits.Count;

                var tasks = Subscribers.Keys
                    .Select( subscriber =>
                    { 
                        return subscriber
                            .QueuedMessages()
                            .ContinueWith( t =>
                            {
                                Subscribers[subscriber].MessagesProcessed += count;
                                projectionProgressAction.Do( action => action( subscriber, Subscribers[subscriber] ) );
                            } );
                    } )
                    .ToArray();
                Task.WaitAll( tasks );

                    //    .ToList()
                    //    .ForEach( task => task
                    //        .ContinueWith( t =>
                    //        {

                    //        } ) );

                    //EventSequence.OnFinish( null, ( p, id ) =>
                    //{
                    //    if ( Subscribers.ContainsKey( p ) )
                    //    {
                    //        Subscribers[p].MessagesProcessed += count;
                    //        projectionProgressAction( p, Subscribers[p] );
                    //    }
                    //} );
                //}
                //else
                //{
                //    Subscribers.ToList().ForEach( p => p.Value.MessagesProcessed += count );
                //}
                //if ( commits.Any() )
                //{
                //    EventSequence.Wait( projections, EventStoreConstants.RebuildSessionIdentity );
                //}
            }
        }

        #endregion
        
        public ReplayHistoryService(
            IStoreEvents store,
            IEventPublisher eventPublisher,
            ILogger<ReplayHistoryService> logger,
            ICommitsIterator iterator = null )
        {
            EventPublisher = eventPublisher;
            Logger = logger;
            Iterator = iterator ?? new CommitsIteratorByPeriod( store );
        }

        public void SetIterator( ICommitsIterator iterator )
        {
            Iterator = iterator;
        }

        public void CleanHistory( List<IEventSubscriber> projections = null )
        {
            lock ( LockRebuild )
            {
                if ( projections != null && projections.Any() )
                {
                    foreach ( var model in projections )
                        model.Handle( new SystemCleanedUpEvent() );
                }
                else
                {
                    EventPublisher.Publish( new SystemCleanedUpEvent() );
                }
            }
        }

        public void Rebuild( 
            List<IEventSubscriber> projections, 
            Action finishAllAction = null,
            Action errorAction = null,
            Action<IEventSubscriber> finishSubscriberAction = null,
            Action<IEventSubscriber, ProjectionRebuildInfo> subscriberProgressAction = null )
        {
            lock ( LockRebuild )
            {
                if ( Status != RebuildStatus.Ready )
                    throw new InvalidOperationException( "Can't start two rebuild session" );

                Status = RebuildStatus.Started;
                Subscribers = projections.ToDictionary( p => p, p => new ProjectionRebuildInfo { MessagesProcessed = 0 } );
            }

            Iterator.Reset();
            var task = new Task( () =>
            {
                try
                {
                    RebuildInternal( projections, Iterator, subscriberProgressAction );
                    Status = RebuildStatus.WaitingForProjections;
                    var tasks = Subscribers.Keys
                        .Select( subscriber =>
                        {
                            return subscriber
                                .QueuedMessages()
                                .ContinueWith( t =>
                                {
                                    finishSubscriberAction.Do( action => action( subscriber ) );
                                    Subscribers[subscriber].Done = true;
                                } );
                        } )
                        .ToArray();
                    Task.WaitAll( tasks );
                    finishAllAction.Do( a => a() );
                    Status = RebuildStatus.Ready;

                    //EventSequence.OnFinish(
                    //    id =>
                    //    {
                    //        finishAllAction.Do( a => a() );
                    //        Status = RebuildStatus.Ready;
                    //    },
                    //    ( projection, id ) => finishSubscriberAction.Do( a => a( projection ) ),
                    //    projections,
                    //    EventStoreConstants.RebuildSessionIdentity );
                }
                catch ( Exception ex )
                {
                    Status = RebuildStatus.Ready;
                    Logger.Error( "Rebuild error", ex );
                    errorAction.Do( action => action() );
                }
            });
            task.Start();
        }

        public bool IsRebuilding()
        {
            return Status != RebuildStatus.Ready;
        }

        public Dictionary<IEventSubscriber, ProjectionRebuildInfo> GetSubscribersUnderRebuild()
        {
            switch ( Status )
            {
                case RebuildStatus.Started:
                    return Subscribers;
                case RebuildStatus.WaitingForProjections:
                    return Subscribers
                        .Where( kvp => !kvp.Value.Done )
                        .ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
                //return EventSequence
                //    .GetActiveProjections( EventStoreConstants.RebuildSessionIdentity )
                //    .ToDictionary( p => p, p => Subscribers[p] );
                // ReSharper disable RedundantCaseLabel
                case RebuildStatus.Ready:
// ReSharper restore RedundantCaseLabel
                default:
                    return new Dictionary<IEventSubscriber, ProjectionRebuildInfo>();
            }
        }
    }
}
