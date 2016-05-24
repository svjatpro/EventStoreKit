using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Runtime.Remoting.Messaging;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Sql.ProjectionTemplates;
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
        
        protected ProjectionBase( ILog logger, IScheduler scheduler )
            : base( logger, scheduler )
        {
            Register<SystemCleanedUpEvent>( DoCleanUp );
        }
        
        protected void RegisterTemplate<TTemplate>( TTemplate template ) where TTemplate : IProjectionTemplate
        {
            ProjectionTemplates.Add( template );
        }

        protected virtual void OnCleanup( SystemCleanedUpEvent message ){ }
        protected virtual void OnIddle() { }
        
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
        /// <param name="message">Initial message</param>
        protected void Execute<TArgs>( EventHandler<TArgs> @event, object sender, TArgs args, Message message = null ) where TArgs : EventArgs
        {
            if ( @event != null && !IsRebuild && ( message == null || !message.IsBulk ) )
                @event.BeginInvoke( sender, args, result =>
                {
                    try { ( (EventHandler<TArgs>)( (AsyncResult)result ).AsyncDelegate ).EndInvoke( result ); }
                    catch ( Exception ex )
                    {
                        Log.Error( string.Format( "Error occured during processing '{0}' in '{1}': '{2}'", @event.GetType().Name, GetType().Name, ex.Message ), ex );
                    }
                }, null );
        }
        protected void Execute( EventHandler @event, object sender, EventArgs args, Message message = null )
        {
            if ( @event != null && !IsRebuild && ( message == null || !message.IsBulk ) )
            {
                @event.BeginInvoke( sender, args, result =>
                {
                    try { ( (EventHandler)( (AsyncResult)result ).AsyncDelegate ).EndInvoke( result ); }
                    catch ( Exception ex )
                    {
                        Log.Error( string.Format( "Error occured during processing '{0}' in '{1}': '{2}'", @event.GetType().Name, GetType().Name, ex.Message ), ex );
                    }
                }, null );
            }
        }
    }
}