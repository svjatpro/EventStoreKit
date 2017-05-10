using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            foreach ( var model in projections )
                model.Handle( new SystemCleanedUpEvent() );

            var dateFrom = new DateTime( 2015, 1, 1 );
            Status = RebuildStatus.WaitingForProjections;
            while ( dateFrom <= DateTime.Now )
            {
                //var dateTo = dateFrom.AddMonths( 1 );
                var dateTo = dateFrom.AddYears( 1 );
                var query =
                    dateTo > DateTime.Now ? 
                    Store.Advanced.GetFrom( dateFrom ) : 
                    Store.Advanced.GetFromTo( "default", dateFrom, dateTo );
                var commits = query.OrderBy( c => c.CommitStamp ).ThenBy( c => c.CheckpointToken ).ToList();

                foreach ( var commit in commits )
                {
                    if ( commit.Headers.Keys.Any( s => s == "SagaType" ) )
                        continue;
                    foreach ( var model in projections )
                        ReplayCommit( commit, model );
                }
                dateFrom = dateTo;
                if( dateFrom <= DateTime.Now )
                    EventSequence.Wait( projections, EventStoreConstants.RebuildSessionIdentity );
            }
        }

        #endregion

        public ReplayHistoryService( IStoreEvents store, IEventPublisher eventPublisher, EventSequence eventSequence )
        {
            Store = store;
            EventPublisher = eventPublisher;
            EventSequence = eventSequence;
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

        public void Rebuild( List<IProjection> projections, Action finishAllAction = null, Action<IProjection> finishProjectionAction = null )
        {
            lock ( LockRebuild )
            {
                if ( Status != RebuildStatus.Ready )
                    throw new InvalidOperationException( "Can't start two rebuild session" );

                Status = RebuildStatus.Started;
                Projections = projections.ToList();
            }

            var task = new Task( () =>
            {
                RebuildInternal( projections );
                EventSequence.OnFinish(
                    id =>
                    {
                        finishAllAction.Do( a => a() );
                        Status = RebuildStatus.Ready;
                    },
                    ( projection, id ) => finishProjectionAction.Do( a => a( projection ) ),
                    projections,
                    EventStoreConstants.RebuildSessionIdentity );
            });
            task.Start();

            //EventSequence.OnFinish(
            //    id =>
            //    {
            //        finishAllAction.Do( a => a() );
            //        Status = RebuildStatus.Ready;
            //    },
            //    ( projection, id ) => finishProjectionAction.Do( a => a( projection ) ),
            //    projections,
            //    EventStoreConstants.RebuildSessionIdentity );
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
