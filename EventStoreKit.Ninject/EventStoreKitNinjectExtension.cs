using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;

namespace EventStoreKit.Ninject
{

    public static class EventStoreKitNinjectExtension
    {
        //public static void InitializeEventStoreKitService( this ContainerBuilder builder,
        //    Action<IComponentContext, IEventStoreKitServiceBuilder> serviceInitializer )
        //{
        //    builder.InitializeEventStoreKitService( null, serviceInitializer );
        //}

        //public static void InitializeEventStoreKitService(
        //    this ContainerBuilder builder,
        //    Action<IEventStoreKitServiceBuilder> preInitialize = null,
        //    Action<IComponentContext, IEventStoreKitServiceBuilder> initialize = null )
        //{

        //    // create service instance
        //    var service = new EventStoreKitService( false );
        //    preInitialize.Do( creator => creator( service ) );
            
        //    // register service
        //    builder
        //        .Register( ctx =>
        //        {
        //            initialize.Do( initializer => initializer( ctx, service ) );

                    

        //            service.Initialize();
        //            return service;
        //        } )
        //        .As<IEventStoreKitService>()
        //        //.As<ICommandBus>()
        //        //.As<IEventPublisher>()
        //        .AutoActivate()
        //        .SingleInstance()
        //        .OwnedByLifetimeScope();
        //}
    }
}