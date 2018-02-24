using System;
using System.Collections.Generic;
using EventStoreKit.Messages;

namespace EventStoreKit.Projections
{
    public class MessageEventArgs : EventArgs
    {
        public readonly Message Message;

        public MessageEventArgs( Message message )
        {
            Message = message;
        }
    }

    public interface IEventSubscriber
    {
        /// <summary>
        /// Handle message
        /// </summary>
        void Handle( Message message );

        /// <summary>
        /// Replay ( repeat ) message
        /// </summary>
        void Replay( Message message );

        /// <summary>
        /// Message types, which can be handled by subscriber
        /// </summary>
        IEnumerable<Type> HandledEventTypes { get; }

        /// <summary>
        /// Occurese on each message handled
        /// </summary>
        event EventHandler<MessageEventArgs> MessageHandled;

        /// <summary>
        /// Occurese on each SequenceMarkerEvent handled
        /// </summary>
        event EventHandler<MessageEventArgs> MessageSequenceHandled;
    }
}