using EventStoreKit.DbProviders;
using EventStoreKit.Messages;
using EventStoreKit.Projections;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Services
{
    public interface IEventStoreKitService
    {
        IEventStoreConfiguration GetConfiguration();
        TSubscriber GetSubscriber<TSubscriber>() where TSubscriber : IEventSubscriber;
        IDbProviderFactory GetDataBaseProviderFactory<TModel>();

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