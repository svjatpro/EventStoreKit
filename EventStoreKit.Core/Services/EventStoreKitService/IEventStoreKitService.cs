using System;
using EventStoreKit.Messages;
using EventStoreKit.Projections;

namespace EventStoreKit.Services
{
    public interface IEventStoreKitService : IDisposable
    {
        TSubscriber GetSubscriber<TSubscriber>() where TSubscriber : IEventSubscriber;
        
        void SendCommand( DomainCommand command );

        void RaiseEvent( DomainEvent message );
        void Publish( DomainEvent message );

        /// <summary>
        /// Wait for all subscribers
        /// </summary>
        void Wait( params IEventSubscriber[] subscribers );

        /// <summary>
        /// Run it on your own risk, it will remove all data from EventStore and projections!
        /// </summary>
        void CleanData();
        // CleanData for specific bucket / stream

        // Rebuild
        // Rebuild for projection
    }
}