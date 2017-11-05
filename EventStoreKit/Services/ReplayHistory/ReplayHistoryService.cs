using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStoreKit.Constants;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services.ReplayHistory;
using EventStoreKit.Utility;
using NEventStore;
using NEventStore.Persistence;

namespace EventStoreKit.Services
{
    public class ReplayHistoryService : IReplaysHistory
    {
        #region Private fields

        private const string SagaTypeHeader = "SagaType";
        //private readonly IStoreEvents Store;
        private readonly IEventPublisher EventPublisher;
        private readonly EventSequence EventSequence;
        private readonly ILogger<ReplayHistoryService> Logger;

        private readonly object LockRebuild = new object();
        private Dictionary<IProjection, ProjectionRebuildInfo> Projections;
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
                    //SetTimestamp( e, commit );
                    model.Replay( e );
                } );
            }
        }

        private void RebuildInternal( 
            List<IProjection> projections,
            ICommitsIterator iterator, 
            Action<IProjection, ProjectionRebuildInfo> projectionProgressAction )
        {
            foreach ( var model in projections )
                model.Handle( new SystemCleanedUpEvent() );

            //var commits = LoadNextCommits( interval );
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
                //commits = LoadNextCommits( interval );
                commits = iterator.LoadNext();
                var count = commits.Count;
                if ( projectionProgressAction != null )
                {
                    EventSequence.OnFinish( null, ( p, id ) =>
                    {
                        if ( Projections.ContainsKey( p ) )
                        {
                            Projections[p].MessagesProcessed += count;
                            projectionProgressAction( p, Projections[p] );
                        }
                    } );
                }
                else
                {
                    Projections.ToList().ForEach( p => p.Value.MessagesProcessed += count );
                }
                if ( commits.Any() )
                {
                    EventSequence.Wait( projections, EventStoreConstants.RebuildSessionIdentity );
                }
            }
        }

        #endregion
        
        public ReplayHistoryService( IStoreEvents store, IEventPublisher eventPublisher, EventSequence eventSequence, ILogger<ReplayHistoryService> logger )
        {
            //Store = store;
            EventPublisher = eventPublisher;
            EventSequence = eventSequence;
            Logger = logger;
        }

        public void CleanHistory( List<IProjection> projections = null )
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
            List<IProjection> projections, 
            Action finishAllAction = null,
            Action errorAction = null,
            Action<IProjection> finishProjectionAction = null,
            Action<IProjection, ProjectionRebuildInfo> projectionProgressAction = null,
            ICommitsIterator iterator = null )
            //ReplayHistoryInterval interval = ReplayHistoryInterval.Length )
        {
            lock ( LockRebuild )
            {
                if ( Status != RebuildStatus.Ready )
                    throw new InvalidOperationException( "Can't start two rebuild session" );

                Status = RebuildStatus.Started;
                Projections = projections.ToDictionary( p => p, p => new ProjectionRebuildInfo { MessagesProcessed = 0 });
            }

            var task = new Task( () =>
            {
                try
                {
                    //RebuildInternal( projections, interval, projectionProgressAction );
                    RebuildInternal( projections, iterator, projectionProgressAction );
                    Status = RebuildStatus.WaitingForProjections;
                    EventSequence.OnFinish(
                        id =>
                        {
                            finishAllAction.Do( a => a() );
                            Status = RebuildStatus.Ready;
                        },
                        ( projection, id ) => finishProjectionAction.Do( a => a( projection ) ),
                        projections,
                        EventStoreConstants.RebuildSessionIdentity );
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

        public Dictionary<IProjection, ProjectionRebuildInfo> GetProjectionsUnderRebuild()
        {
            switch ( Status )
            {
                case RebuildStatus.Started:
                    return Projections;
                case RebuildStatus.WaitingForProjections:
                    return EventSequence
                        .GetActiveProjections( EventStoreConstants.RebuildSessionIdentity )
                        .ToDictionary( p => p, p => Projections[p] );
// ReSharper disable RedundantCaseLabel
                case RebuildStatus.Ready:
// ReSharper restore RedundantCaseLabel
                default:
                    return new Dictionary<IProjection, ProjectionRebuildInfo>();
            }
        }

        public bool IsEventLogEmpty
        {
            get { return !Store.Advanced.GetFrom( new DateTime( 2010, 1, 1 ) ).Any(); }
        }

        public IEnumerable<ICommit> GetCommits()
        {
            return Store.Advanced.GetFromStart().ToList();
        }
        
    }
}
