using EventStoreKit.DbProviders;
using EventStoreKit.Messages;
using EventStoreKit.Projections;

namespace EventStoreKit.Services
{
    public interface IEventStoreKitService
    {
        TSubscriber ResolveSubscriber<TSubscriber>() where TSubscriber : IEventSubscriber;
        IDbProviderFactory ResolveDbProviderFactory<TModel>();

        void SendCommand( DomainCommand command );

        void RaiseEvent( DomainEvent message );
        void Publish( DomainEvent message );
        void Wait( params IEventSubscriber[] subscribers );
    }
}