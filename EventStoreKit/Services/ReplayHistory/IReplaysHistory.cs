using System;
using System.Collections.Generic;
using EventStoreKit.Projections;
using EventStoreKit.Services.ReplayHistory;
using NEventStore;

namespace EventStoreKit.Services
{
    public class ProjectionRebuildInfo
    {
        public bool Done { get; set; }
        public decimal MessagesProcessed { get; set; }
    }

    public interface IReplaysHistory
    {
        bool IsEventLogEmpty { get; }
        IEnumerable<ICommit> GetCommits();

        void CleanHistory( List<IProjection> projections );
        void Rebuild( 
            List<IProjection> projections, 
            Action finishAllAction = null, 
            Action errorAction = null, 
            Action<IProjection> finishProjectionAction = null,
            Action<IProjection, ProjectionRebuildInfo> projectionProgressAction = null,
            ICommitsIterator strategy = null );
        bool IsRebuilding();
        Dictionary<IProjection, ProjectionRebuildInfo> GetProjectionsUnderRebuild();
    }
}
