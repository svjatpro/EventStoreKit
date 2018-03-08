using System;
using System.Collections.Generic;
using EventStoreKit.Messages;
using NEventStore;

namespace EventStoreKit.Utility
{
    public static class EventStoreUtility
    {
        public static void ProcessEvent( this EventMessage evt, Action<DomainEvent> eventProcessingMethod )
        {
            if ( evt.Body is IEnumerable<DomainEvent> )
            {
                foreach ( var item in evt.Body as IEnumerable<DomainEvent> )
                    eventProcessingMethod( item );
            }
            else
            {
                eventProcessingMethod( evt.Body as DomainEvent );
            }
        }
    }
}