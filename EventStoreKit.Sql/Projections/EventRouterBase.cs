using System;
using System.Reactive.Concurrency;
using EventStoreKit.Logging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.IdGenerators;
using EventStoreKit.Utility;
using NEventStore;

namespace EventStoreKit.Sql.Projections
{
    public abstract class EventRouterBase : ProjectionBase, IEventRouter
    {
        private readonly IStoreEvents StoreEvents;
        private readonly IIdGenerator IdGenerator;
        private readonly ICurrentUserProvider CurrentUserProvider;

        protected EventRouterBase( ILogger logger, IScheduler scheduler, IStoreEvents storeEvents, IIdGenerator idGenerator, ICurrentUserProvider currentUserProvider )
            : base( logger, scheduler )
        {
            StoreEvents = storeEvents;
            IdGenerator = idGenerator;
            CurrentUserProvider = currentUserProvider;
        }

        protected void RaiseEvent<TEvent>( TEvent @event ) where TEvent : Message
        {
            var e = (@event as DomainEvent);
            if ( e != null )
            {
                if ( e.CreatedBy == Guid.Empty && CurrentUserProvider.CurrentUserId != null )
                    e.CreatedBy = CurrentUserProvider.CurrentUserId.Value;
            }
            if ( !IsRebuild )
            {
                if ( @event.Created == default( DateTime ) || @event.Created <= DateTime.MinValue )
                    @event.Created = DateTime.Now.TrimMilliseconds();
                using ( var stream = StoreEvents.CreateStream( /*(?const?) RouterId*/ IdGenerator.NewGuid() ) )
                {
                    stream.Add( new EventMessage {Body = @event} );
                    stream.CommitChanges( IdGenerator.NewGuid() );
                }
            }
        }

        public override void Replay( Message e )
        {
            Handle( e, true );
        }
    }
}
