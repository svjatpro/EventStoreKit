using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Projections;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Services
{
    public interface IEventStoreKitServiceBuilder
    {
        ServiceProperty<IEventStoreConfiguration> Configuration { get; }
        ServiceProperty<ILoggerFactory> LoggerFactory { get; }
        ServiceProperty<IScheduler> Scheduler { get; }
        ServiceProperty<IDbProviderFactory> DbProviderFactorySubscriber { get; }
        ServiceProperty<IDbProviderFactory> DbProviderFactoryEventStore { get; }

        Dictionary<Type, Func<IEventSubscriber>> GetEventSubscribers();

        IEventStoreKitServiceBuilder SetConfiguration( IEventStoreConfiguration configuration );
        IEventStoreKitServiceBuilder SetLoggerFactory( ILoggerFactory factory );
        IEventStoreKitServiceBuilder SetScheduler( IScheduler scheduler );

        IEventStoreKitServiceBuilder SetDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration ) where TDbProviderFactory : IDbProviderFactory;
        IEventStoreKitServiceBuilder SetDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration );
        IEventStoreKitServiceBuilder SetDataBase( IDbProviderFactory factory );
        IEventStoreKitServiceBuilder SetSubscriberDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration );
        IEventStoreKitServiceBuilder SetSubscriberDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration );
        IEventStoreKitServiceBuilder SetSubscriberDataBase( IDbProviderFactory factory );
        IEventStoreKitServiceBuilder SetEventStoreDataBase<TDbProviderFactory>( IDataBaseConfiguration configuration );
        IEventStoreKitServiceBuilder SetEventStoreDataBase( Type dbProviderFactoryType, IDataBaseConfiguration configuration );
        IEventStoreKitServiceBuilder SetEventStoreDataBase( IDbProviderFactory factory );

        IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory ) where TSubscriber : class, IEventSubscriber;
        IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory, IDataBaseConfiguration configuration ) where TSubscriber : class, IEventSubscriber;
        IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( Func<IEventStoreSubscriberContext, TSubscriber> subscriberFactory, IDbProviderFactory dbProviderFactory ) where TSubscriber : class, IEventSubscriber;
        IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( IDataBaseConfiguration configuration ) where TSubscriber : class, IEventSubscriber;
        IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>( IDbProviderFactory dbProviderFactory ) where TSubscriber : class, IEventSubscriber;
        IEventStoreKitServiceBuilder RegisterEventSubscriber<TSubscriber>() where TSubscriber : class, IEventSubscriber;
        IEventStoreKitServiceBuilder RegisterEventSubscriber( Func<IEventSubscriber> subscriberFactory );

        IEventStoreKitServiceBuilder RegisterCommandHandler<THandler>() where THandler : class, ICommandHandler, new();
        IEventStoreKitServiceBuilder RegisterCommandHandler( Func<ICommandHandler> handlerFactory );

        IEventStoreKitService Initialize();
    }
}