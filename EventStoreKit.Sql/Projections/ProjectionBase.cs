using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Sql.ProjectionTemplates;
using EventStoreKit.Utility;
using log4net;

namespace EventStoreKit.Sql.Projections
{
    public abstract class ProjectionBase : EventQueueSubscriber, IProjection
    {
        #region Private fields

        protected readonly List<IProjectionTemplate> ProjectionTemplates = new List<IProjectionTemplate>();
        
        #endregion

        #region Private methods

        private void DoCleanUp( SystemCleanedUpEvent message )
        {
            foreach ( var projectionTemplate in ProjectionTemplates )
                projectionTemplate.CleanUp( message );
            OnCleanup( message );
        }
        
        #endregion

        #region Private event handlers

        private void Apply( SequenceMarkerEvent msg )
        {
            OnSequenceFinished( msg );
            SequenceFinished.Execute( this, new SequenceEventArgs( msg.Identity ) );
        }

        #endregion

        public event EventHandler<SequenceEventArgs> SequenceFinished;
        
        protected ProjectionBase( ILog logger, IScheduler scheduler )
            : base( logger, scheduler )
        {
            Register<SystemCleanedUpEvent>( DoCleanUp );
            Register<SequenceMarkerEvent>( Apply );
        }
        
        protected void RegisterTemplate<TTemplate>( TTemplate template ) where TTemplate : IProjectionTemplate
        {
            ProjectionTemplates.Add( template );
        }

        protected abstract void OnCleanup( SystemCleanedUpEvent message );
        protected virtual void OnSequenceFinished( SequenceMarkerEvent message ){}

        public abstract string Name { get; }

        protected override void PreprocessMessage( Message message )
        {
            ProjectionTemplates.ForEach( t => t.PreprocessEvent( message ) );
        }

        /// <summary>
        /// Executes .net events in secure way: 
        ///  - check if there is any subscribers
        ///  - prevent execution during rebuild
        ///  - prevent execution for bulk messages
        /// </summary>
        /// <param name="event">Event handler</param>
        /// <param name="sender">Sender object</param>
        /// <param name="args">Generic event argument</param>
        /// <param name="message"></param>
        protected void Execute<TArgs>( EventHandler<TArgs> @event, object sender, TArgs args, Message message = null ) where TArgs : EventArgs
        {
            if ( @event != null && !IsRebuild && ( message == null || !message.IsBulk ) )
                @event.Invoke( sender, args );
        }
        protected void Execute( EventHandler @event, object sender, EventArgs args, Message message = null )
        {
            if ( @event != null && !IsRebuild && ( message == null || !message.IsBulk ) )
                @event.Invoke( sender, args );
        }
    }
}