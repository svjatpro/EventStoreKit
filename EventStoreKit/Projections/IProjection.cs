
using System;

namespace EventStoreKit.Projections
{
    public class SequenceEventArgs : EventArgs
    {
        public readonly Guid SequenceIdentity;

        public SequenceEventArgs( Guid sequenceIdentity )
        {
            SequenceIdentity = sequenceIdentity;
        }
    }

    public interface IProjection : IEventSubscriber
    {
        event EventHandler<SequenceEventArgs> SequenceFinished;
        string Name { get; }
    }
}
