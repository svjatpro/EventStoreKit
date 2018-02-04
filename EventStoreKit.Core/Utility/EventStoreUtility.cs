using System;
using System.Collections.Generic;
using EventStoreKit.Messages;
using NEventStore;

namespace EventStoreKit.Utility
{
    public static class EventStoreUtility
    {
        public static void ProcessEvent( this EventMessage evt, Action<Message> eventProcessingMethod )
        {
            if ( evt.Body is IEnumerable<Message> )
            {
                foreach ( var item in evt.Body as IEnumerable<Message> )
                    eventProcessingMethod( item );
            }
            else
            {
                eventProcessingMethod( evt.Body as Message );
            }
        }
    }
}