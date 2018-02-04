using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Utility;

namespace EventStoreKit.Services
{
    public class EventSequence
    {
        #region Private fields

        internal class SessionInfo
        {
            public readonly Guid Identity;
            public readonly bool Async;
            public readonly Dictionary<IEventSubscriber,bool> Projections;
            public readonly Action<Guid> FinishAllAction;
            public readonly Action<IEventSubscriber,Guid> FinishProjectionAction;

            public SessionInfo(
                Guid identity,
                IEnumerable<IEventSubscriber> subscribers,
                bool @async = false,
                Action<Guid> finishAllAction = null,
                Action<IEventSubscriber, Guid> finishProjectionAction = null )
            {
                Identity = identity;
                FinishAllAction = finishAllAction;
                FinishProjectionAction = finishProjectionAction;
                Async = async;
                Projections = subscribers.ToDictionary( p => p, p => true );
            }
        }

        private readonly IEventPublisher EventPublisher;
        private readonly List<IEventSubscriber> AllSubscribers;

        private readonly ConcurrentDictionary<Guid, SessionInfo> SessionCache = new ConcurrentDictionary<Guid, SessionInfo>();

        #endregion

        #region Private methods

        private void OnProjectionSequenceFinished( object sender, SequenceEventArgs e )
        {
            var projection = sender as IEventSubscriber;
            var identity = e.SequenceIdentity;
            SessionInfo session;
            SessionCache.TryGetValue( e.SequenceIdentity, out session );
            if ( session == null || projection == null )
                return;

            lock ( session )
            {
                if ( session.Projections.ContainsKey( projection ) && session.Projections[projection] )
                {
                    session.FinishProjectionAction.Do( action => action( projection, identity ) );
                    session.Projections[projection] = false;

                    if ( !session.Projections.Any( p => p.Value ) )
                    {
                        session.FinishAllAction.Do( action => action( identity ) ); // todo: send rebuild time
                        SessionCache.TryRemove( e.SequenceIdentity, out session );
                        if( session.Async )
                            Monitor.Pulse( session );
                    }
                }
            }
        }

        #endregion

        public EventSequence( IEnumerable<IEventSubscriber> projections, IEventPublisher eventPublisher )
        {
            AllSubscribers = projections.ToList();
            EventPublisher = eventPublisher;
            
            AllSubscribers.ForEach( p => p.SequenceFinished += OnProjectionSequenceFinished );
        }

        public void Wait( List<IEventSubscriber> projections = null, Guid? identity = null )
        {
            var id = identity ?? Guid.NewGuid();
            var projectionsToRebuild = projections ?? AllSubscribers;
            var session = new SessionInfo( id, projectionsToRebuild, true );
            SessionCache.TryAdd( id, session );

            lock ( session )
            {
                //EventPublisher.Publish( new SequenceMarkerEvent { Identity = id } );
                projectionsToRebuild.ForEach( p => p.Handle( new SequenceMarkerEvent { Identity = id } ) );
                Monitor.Wait( session );
            }
        }

        public Guid OnFinish( Action<Guid> finishAll, Action<IEventSubscriber,Guid> finishProjection = null, List<IEventSubscriber> projections = null, Guid? identity = null )
        {
            var id = identity ?? Guid.NewGuid();
            var projectionsToWait = projections ?? AllSubscribers;

            if ( SessionCache.TryAdd( id, new SessionInfo( id, projectionsToWait, finishAllAction: finishAll, finishProjectionAction: finishProjection ) ) )
            {
                //EventPublisher.Publish( new SequenceMarkerEvent{ Identity = id } );
                projectionsToWait.ForEach( p => p.Handle( new SequenceMarkerEvent {Identity = id} ) );
            }

            return id;
        }

        public IEnumerable<IEventSubscriber> GetActiveProjections( Guid identity )
        {
            SessionCache.TryGetValue( identity, out var session );
            if( session == null )
                return new IProjection[]{};

            lock ( session )
            {
                return session.Projections.Where( p => p.Value ).Select( p => p.Key ).ToList();
            }
        }
    }
}
