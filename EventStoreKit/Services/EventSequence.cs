using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Monads;
using System.Threading;
using EventStoreKit.Messages;
using EventStoreKit.Projections;

namespace EventStoreKit.Services
{
    public class EventSequence
    {
        #region Private fields

        internal class SessionInfo
        {
            public readonly Guid Identity;
            public readonly bool Async;
            public readonly Dictionary<IProjection,bool> Projections;
            public readonly Action<Guid> FinishAllAction;
            public readonly Action<IProjection,Guid> FinishProjectionAction;

            public SessionInfo(
                Guid identity,
                IEnumerable<IProjection> projections,
                bool @async = false,
                Action<Guid> finishAllAction = null,
                Action<IProjection, Guid> finishProjectionAction = null )
            {
                Identity = identity;
                FinishAllAction = finishAllAction;
                FinishProjectionAction = finishProjectionAction;
                Async = async;
                Projections = projections.ToDictionary( p => p, p => true );
            }
        }

        private readonly IEventPublisher EventPublisher;
        private readonly List<IProjection> AllProjections;

        private readonly ConcurrentDictionary<Guid, SessionInfo> SessionCache = new ConcurrentDictionary<Guid, SessionInfo>();

        #endregion

        #region Private methods

        private void OnProjectionSequenceFinished( object sender, SequenceEventArgs e )
        {
            var projection = sender as IProjection;
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

        public EventSequence( IEnumerable<IProjection> projections, IEventPublisher eventPublisher )
        {
            AllProjections = projections.ToList();
            EventPublisher = eventPublisher;
            
            AllProjections.ForEach( p => p.SequenceFinished += OnProjectionSequenceFinished );
        }

        public void Wait( List<IProjection> projections = null, Guid? identity = null )
        {
            var id = identity ?? Guid.NewGuid();
            var projectionsToRebuild = projections ?? AllProjections;
            var session = new SessionInfo( id, projectionsToRebuild, true );
            SessionCache.TryAdd( id, session );

            lock ( session )
            {
                //EventPublisher.Publish( new SequenceMarkerEvent { Identity = id } );
                projectionsToRebuild.ForEach( p => p.Handle( new SequenceMarkerEvent { Identity = id } ) );
                Monitor.Wait( session );
            }
        }

        public Guid OnFinish( Action<Guid> finishAll, Action<IProjection,Guid> finishProjection = null, List<IProjection> projections = null, Guid? identity = null )
        {
            var id = identity ?? Guid.NewGuid();
            var projectionsToRebuild = projections ?? AllProjections;

            if ( SessionCache.TryAdd( id, new SessionInfo( id, projectionsToRebuild, finishAllAction: finishAll, finishProjectionAction: finishProjection ) ) )
            {
                //EventPublisher.Publish( new SequenceMarkerEvent{ Identity = id } );
                projectionsToRebuild.ForEach( p => p.Handle( new SequenceMarkerEvent {Identity = id} ) );
            }

            return id;
        }

        public IEnumerable<IProjection> GetActiveProjections( Guid identity )
        {
            SessionInfo session;
            SessionCache.TryGetValue( identity, out session );
            if( session == null )
                return new IProjection[]{};

            lock ( session )
            {
                return session.Projections.Where( p => p.Value ).Select( p => p.Key ).ToList();
            }
        }
    }
}
