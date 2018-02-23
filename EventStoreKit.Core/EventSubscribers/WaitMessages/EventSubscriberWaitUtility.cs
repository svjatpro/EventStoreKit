﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Utility;

namespace EventStoreKit.Core.EventSubscribers
{
    public static class EventSubscriberWaitUtility
    {
        /// <summary>
        /// Wait until all messages, which are in EventSubscriber queue at the moment of the method call, will be processed
        ///  key point here, that there is guarantee, that each IEventSubscriber instance have its own message queue and process it synchronously
        /// </summary>
        public static void WaitMessages( this IEventSubscriber subscriber )
        {
            var identity = Guid.NewGuid();
            var sequenceMarker = subscriber.When<SequenceMarkerEvent>( msg => msg.Identity == identity );
            subscriber.Handle( new SequenceMarkerEvent{ Identity = identity } );

            sequenceMarker.Wait();
        }

        /// <summary>
        /// Create a trigger for single message processed by subscriber, which mutches provided conditions
        /// </summary>
        public static Task<Message> When( this IEventSubscriber subscriber, Func<Message, bool> predicat )
        {
            return subscriber.When<Message>( predicat );
        }
        /// <summary>
        /// Create a trigger for single typed message processed by subscriber, which mutches provided conditions
        /// </summary>
        public static Task<TMessage> When<TMessage>( this IEventSubscriber subscriber, Func<TMessage,bool> predicat ) where TMessage : Message
        {
            return subscriber
                .When( MessageMatch.Is( predicat ) )
                .ContinueWith( task =>
                {
                    if( task.IsFaulted )
                        throw task.Exception;
                    return task.Result.FirstOrDefault().OfType<TMessage>();
                } );
        }

        /// <summary>
        /// Create a trigger for messages processed by subscriber, which mutches provided conditions
        /// </summary>
        public static Task<List<Message>> When( this IEventSubscriber subscriber, MessageMatch match )
        {
            match.Start();
            void Handler( object o, MessageEventArgs arg )
            {
                // this is strongly required, 
                // because MessageHandled event executed asyncshronously, and MessageMatch is not thread-safe
                lock( match )
                {
                    match.ProcessMessage( arg.Message );
                    if ( match.TaskCompletionSource.Task.IsCompleted )
                        subscriber.MessageHandled -= Handler;
                }
            }
            subscriber.MessageHandled += Handler;
            return match.TaskCompletionSource.Task;
        }
    }
}
