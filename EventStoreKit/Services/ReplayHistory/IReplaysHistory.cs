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
        void CleanHistory( List<IEventSubscriber> subscribers );
        void Rebuild( 
            List<IEventSubscriber> projections, 
            Action finishAllAction = null, 
            Action errorAction = null, 
            Action<IEventSubscriber> finishSubscriberAction = null,
            Action<IEventSubscriber, ProjectionRebuildInfo> subscriberProgressAction = null );
        bool IsRebuilding();
        Dictionary<IEventSubscriber, ProjectionRebuildInfo> GetSubscribersUnderRebuild();
    }
}