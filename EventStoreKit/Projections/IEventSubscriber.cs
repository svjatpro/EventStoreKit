using System;
using System.Collections.Generic;
using EventStoreKit.Messages;

namespace EventStoreKit.Projections
{
    public interface IEventSubscriber
    {
        void Handle( Message e );
        void Replay( Message e );
        IEnumerable<Type> HandledEventTypes { get; }
    }
}