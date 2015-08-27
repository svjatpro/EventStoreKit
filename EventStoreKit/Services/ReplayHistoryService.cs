using System;
using System.Collections.Generic;
using System.Linq;
using EventStoreKit.Constants;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Utility;
using NEventStore;
using NEventStore.Persistence;

namespace EventStoreKit.Services
{
    public class ReplayHistoryService : IReplaysHistory
    {
        #region Private fields

        private readonly IStoreEvents Store;
        private readonly IEventPublisher EventPublisher;
        private readonly EventSequence EventSequence;

        private readonly object LockRebuild = new object();
        private List<IProjection> Projections;
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

        private void RebuildInternal( List<IProjection> projections )
        {
            lock ( LockRebuild )
            {
                if ( Status != RebuildStatus.Ready )
                    throw new InvalidOperationException( "Can't start two rebuild session" );

                Status = RebuildStatus.Started;
                Projections = projections.ToList();
            }

            var cleanEvent = new SystemCleanedUpEvent();
            foreach ( var model in projections )
                model.Replay( cleanEvent );

            var commits = Store.Advanced.GetFrom( new DateTime( 2010, 1, 1 ) ).ToList();
            foreach ( var model in projections )
                model.Handle( new SystemCleanedUpEvent() );
            foreach ( var commit in commits )
            {
                if ( commit.Headers.Keys.Any( s => s == "SagaType" ) )
                    continue;
                foreach ( var model in projections )
                    ReplayCommit( commit, model );
            }
        }

        #endregion

        public ReplayHistoryService( IStoreEvents store, IEventPublisher eventPublisher, EventSequence eventSequence )
        {
            Store = store;
            EventPublisher = eventPublisher;
            EventSequence = eventSequence;
        }

        public void CleanHistory()
        {
            EventPublisher.Publish( new SystemCleanedUpEvent() );
        }

        public void Rebuild( List<IProjection> projections )
        {
            RebuildInternal( projections );
            Status = RebuildStatus.WaitingForProjections;

            EventSequence.Wait( projections, EventStoreConstants.RebuildSessionIdentity );
            Status = RebuildStatus.Ready;
        }
        public void RebuildAsync( List<IProjection> projections, Action finishAllAction = null, Action<IProjection> finishProjectionAction = null )
        {
            RebuildInternal( projections );
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

        public bool IsRebuilding()
        {
            return Status != RebuildStatus.Ready;
        }

        public List<IProjection> GetProjectionsUnderRebuild()
        {
            switch ( Status )
            {
                case RebuildStatus.Started:
                    return Projections;
                case RebuildStatus.WaitingForProjections:
                    return EventSequence.GetActiveProjections( EventStoreConstants.RebuildSessionIdentity ).ToList();
// ReSharper disable RedundantCaseLabel
                case RebuildStatus.Ready:
// ReSharper restore RedundantCaseLabel
                default:
                    return new List<IProjection>();
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
