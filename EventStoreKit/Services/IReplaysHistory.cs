using System;
using System.Collections.Generic;
using EventStoreKit.Projections;
using NEventStore;

namespace EventStoreKit.Services
{
    public interface IReplaysHistory
    {
        bool IsEventLogEmpty { get; }
        IEnumerable<ICommit> GetCommits();

        void CleanHistory( List<IProjection> projections );
        void Rebuild( List<IProjection> projections, Action finishAllAction = null, Action<IProjection> finishProjectionAction = null, ReplayHistoryInterval interval = ReplayHistoryInterval.Year );
        bool IsRebuilding();
        List<IProjection> GetProjectionsUnderRebuild();
    }
}
