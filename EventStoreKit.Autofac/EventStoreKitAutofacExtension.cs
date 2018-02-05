using System;
using System.Linq;
using Autofac;
using Autofac.Core;
using Autofac.Features.GeneratedFactories;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;

namespace EventStoreKit.Autofac
{
    public static class EventStoreKitAutofacExtension
    {
        public static void InitializeEventStoreKitService( 
            this ContainerBuilder builder, 
            Func<EventStoreKitService> serviceCreator = null,
            Action<IComponentContext, EventStoreKitService> serviceInitializer = null )
        {
            var service = serviceCreator.With( creator => creator() ) ?? new EventStoreKitService();

            // register default EventStoreConfiguration
            builder
                .RegisterInstance(
                    new EventStoreConfiguration
                    {
                        InsertBufferSize = 10000,
                        OnIddleInterval = 500
                    } )
                .As<IEventStoreConfiguration>()
                .IfNotRegistered( typeof(IEventStoreConfiguration) );

            // register default IDbProviderFactory and IDataBaseConfiguration
            var dbProviderFactory = service.GetDataBaseProviderFactory();
            builder
                .RegisterInstance( dbProviderFactory )
                .As<IDbProviderFactory>()
                .IfNotRegistered( typeof(IDbProviderFactory) );
            builder
                .Register( ctx => dbProviderFactory.Create() )
                .As<IDbProviderFactory>()
                .IfNotRegistered( typeof(IDbProviderFactory) );
            builder
                .RegisterInstance( dbProviderFactory.DefaultDataBaseConfiguration )
                .As<IDataBaseConfiguration>()
                .IfNotRegistered( typeof(IDataBaseConfiguration) );

            // register service
            builder
                .Register( ctx =>
                {
                    serviceInitializer.Do( initializer => initializer( ctx, service ) );
                    //var service = initializer.With( initialize => initialize( ctx ) ) ?? new EventStoreKitService();

                    ctx.ResolveOptional<IEventStoreConfiguration>().Do( config => service.SetConfiguration( config ) );

                    // ctx.ResolveKeyed<IDbProviderFactory>( new DataBaseConfiguration( connectionString ) );

                    // Register event handlers
                    var cmdHandlers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( registration => registration.Activator.LimitType.IsAssignableTo<ICommandHandler>() )
                        .Select( registration =>
                        {
                            var factoryGenerator = new FactoryGenerator( typeof(Func<ICommandHandler>), registration,
                                ParameterMapping.ByType );
                            return (Func<ICommandHandler>) factoryGenerator.GenerateFactory( ctx, new Parameter[] { } );
                        } )
                        .Where( h => h != null )
                        .ToList();
                    cmdHandlers.ForEach( handler => service.RegisterCommandHandler( handler ) );

                    // Register event subscribers
                    var subscribers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( r => r.Activator.LimitType.IsAssignableTo<IEventSubscriber>() )
                        .Select( r =>
                            ctx.IsRegistered( r.Activator.LimitType )
                                ? ctx.Resolve( r.Activator.LimitType )
                                : r.Services.FirstOrDefault().With( ctx.ResolveService ) )
                        .Select( h => h.OfType<IEventSubscriber>() )
                        .Where( h => h != null )
                        .ToList();
                    //subscribers.ForEach( s => service.RegisterEventSubscriber(  ) );

                    return service;
                } )
                .As<IEventStoreKitService>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}