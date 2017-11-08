using System;
using System.Collections.Generic;
using EventStoreKit.Projections;
using EventStoreKit.Services.ReplayHistory;

namespace EventStoreKit.Services
{
    public class ProjectionRebuildInfo
    {
        public bool Done { get; set; }
        public decimal MessagesProcessed { get; set; }
    }

    public interface IReplaysHistory
    {
        void SetIterator( ICommitsIterator iterator );
        void CleanHistory( List<IProjection> projections );
        void Rebuild( 
            List<IProjection> projections, 
            Action finishAllAction = null, 
            Action errorAction = null, 
            Action<IProjection> finishProjectionAction = null,
            Action<IProjection, ProjectionRebuildInfo> projectionProgressAction = null );
        bool IsRebuilding();
        Dictionary<IProjection, ProjectionRebuildInfo> GetProjectionsUnderRebuild();
    }
}