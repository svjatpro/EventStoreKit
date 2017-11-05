using System.Collections.Generic;
using NEventStore;

namespace EventStoreKit.Services.ReplayHistory
{
    public interface ICommitsIterator
    {
        void Reset();
        List<ICommit> LoadNext();
    }
}
