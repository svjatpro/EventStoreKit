using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    /// <summary>
    /// temporary interface - should be moved to extension methods, which catch messages through the single MessageHandledEvent
    /// </summary>
    public interface IEventCatch
    {
        #region Dynamic messages catching - it is required to get the exact moment when a subscriber definitely process some messages and client can use projection data

        /// <summary>
        /// waits until messages processed
        /// </summary>
        TMessage CatchMessage<TMessage>( Func<TMessage, bool> handler, int timeout ) where TMessage : Message;
        List<TMessage> CatchMessages<TMessage>( params Func<TMessage, bool>[] handlers ) where TMessage : Message;
        Task<List<TMessage>> CatchMessagesAsync<TMessage>( params Func<TMessage, bool>[] handlers ) where TMessage : Message;

        /// <summary>
        /// Asynchronously waits until messages processed
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        /// <param name="mandatory">mandatory handlers</param>
        /// <param name="optional">optional handlers</param>
        /// <param name="timeout"></param>
        /// <param name="sequence">if true, then mandatory handlers must be processed in sctrict order, otherwise it can be processed in any order</param>
        /// <param name="waitUnprocessed">if true, then after successfull processed of mandatory handlers it will wait until all unrpocessed messages in queue are processed</param>
        /// <returns>The list of matched messages</returns>
        Task<List<TMessage>> CatchMessagesAsync<TMessage>(
            IEnumerable<Func<TMessage, bool>> mandatory,
            IEnumerable<Func<TMessage, bool>> optional,
            int timeout,
            bool sequence = false,
            bool waitUnprocessed = false )
            where TMessage : Message;

        /// <summary>
        /// Sync wait until all messages, which are in EventSubscriber queue at the moment of the method call, will be processed
        ///  key point here, that there is guarantee, that each IEventSubscriber instance have its own message queue and process it synchronously
        /// </summary>
        void WaitMessages();

        /// <summary>
        /// Async wait until all messages, which are in EventSubscriber queue at the moment of the method call, will be processed
        ///  key point here, that there is guarantee, that each IEventSubscriber instance have its own message queue and process it synchronously
        /// </summary>
        Task WaitMessagesAsync();

        #endregion
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