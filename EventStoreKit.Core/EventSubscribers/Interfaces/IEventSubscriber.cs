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

    public class SequenceEventArgs : EventArgs
    {
        public readonly Guid SequenceIdentity;

        public SequenceEventArgs( Guid sequenceIdentity )
        {
            SequenceIdentity = sequenceIdentity;
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

        [Obsolete]
        event EventHandler<SequenceEventArgs> SequenceFinished;

        event EventHandler<MessageEventArgs> MessageHandled;
    }
}