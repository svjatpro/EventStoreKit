using System;
using CommonDomain.Core;
using EventStoreKit.Messages;

namespace EventStoreKit.Aggregates
{
    public abstract class TrackableAggregateBase : AggregateBase, ITrackableAggregate
    {
        public Guid IssuedBy { get; set; }

        protected new void RaiseEvent( object @event )
        {
            var e = ( @event as DomainEvent );
            if ( e != null )
            {
                if( e.Created == default( DateTime ))
                    e.Created = DateTime.Now;
                e.CreatedBy = IssuedBy;
            }
            base.RaiseEvent( @event );
        }
    }
}