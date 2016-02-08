using System;
using CommonDomain;

namespace EventStoreKit.Aggregates
{
    public interface ITrackableAggregate : IAggregate
    {
        Guid IssuedBy { get; set; }
    }
}