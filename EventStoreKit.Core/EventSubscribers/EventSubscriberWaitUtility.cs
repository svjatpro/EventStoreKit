using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Utility;

namespace EventStoreKit.Core.EventSubscribers
{
    public static class EventSubscriberWaitUtility
    {
        //.   CatchMessagesAsync: all args are optional( generally review methods signature )

        //.   extend catching logic

        //-   MessageCatch
        //.Catch(msg => ..., mandatory: true )
        //.Then( msg => ... )
        //.Or( ... )
        //.Then( msg => ..., count = 3 )
        //.AsSequence()
        //.Catch( .. )
        //.Catch( .. )

        //  projection.WaitMessages();
        //  projection.WaitMessage( msg => msg.Id == id1 );
        //  projection.WaitMessage<EntityCreated>( msg => msg.Id == id1 );

        //  await projection.Message<EntityCreated>( msg => msg.Id == id1 ).Handled();
        //  await projection
        //      .When<EntityCreated>( msg => msg.Id == id1 )
        //      .Then<EntityCreated>( msg => msg.Id == id1 )
        //      .Then<EntityCreated>( msg => msg.Id == id1 )
        //      .Handled();

        //  await projection
        //      .Handled<EntityCreated>( msg => msg.Id == id1 )
        //      .Handled<EntityCreated>( msg => msg.Id == id1 )
        //      .Handled<EntityCreated>( msg => msg.Id == id1 );

        //  await projection
        //      .When<EntityCreated>( msg => msg.Id == id1 )
        //      .Then<EntityCreated>( msg => msg.Id == id1 )
        //      .Then<EntityCreated>( msg => msg.Id == id1 )
        //      .Unordered()
        //      .
        //      .Handled();

        public static void WaitMessages1( this IEventSubscriber subscriber )
        {
            var identity = Guid.NewGuid();
            var sequenceMarker = subscriber.When<SequenceMarkerEvent>( msg => msg.Identity == identity );
            subscriber.Handle( new SequenceMarkerEvent{ Identity = identity } );

            sequenceMarker
                .ContinueWith( task => subscriber.When<StreamOnIdleEvent>( msg => true ) )
                .Wait();
        }

        public static Task<TMessage> When<TMessage>( this IEventSubscriber subscriber, Func<TMessage,bool> predicat ) where TMessage : Message
        {
            var taskCompletionSource = new TaskCompletionSource<TMessage>();

            void Handler( object o, MessageEventArgs arg )
            {
                var msg = arg.Message.OfType<TMessage>();
                if ( msg.With( predicat ) )
                {
                    subscriber.MessageHandled -= Handler;
                    taskCompletionSource.SetResult( msg );
                }
            }
            subscriber.MessageHandled += Handler;

            return taskCompletionSource.Task;

            //public readonly TaskCompletionSource<List<TMessage>> TaskCompletionSource = new TaskCompletionSource<List<TMessage>>();
        }

        
    }
}
