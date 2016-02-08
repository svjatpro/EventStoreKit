using System;
using System.Collections.Generic;
using EventStoreKit.Projections;
using NEventStore;

namespace EventStoreKit.Services
{
    public interface IReplaysHistory
    {
        void CleanHistory();
        bool IsEventLogEmpty { get; }
        IEnumerable<ICommit> GetCommits();

        void Rebuild( List<IProjection> projections );
        void RebuildAsync( List<IProjection> projections, Action finishAllAction, Action<IProjection> finishProjectionAction );
        bool IsRebuilding();
        List<IProjection> GetProjectionsUnderRebuild();
    }
}
