using System;
using System.Linq;
using System.Reactive.Concurrency;
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
            // create service instance
            var service = serviceCreator.With( creator => creator() ) ?? new EventStoreKitService( false );

            // register default EventStoreConfiguration
            builder.RegisterInstance( service.Configuration.GetValueOrDefault() )
                .As<IEventStoreConfiguration>()
                .IfNotRegistered( typeof(IEventStoreConfiguration) );

            // register default IDbProviderFactory and IDataBaseConfiguration
            var dbProviderFactory = service.GetDataBaseProviderFactory();
            dbProviderFactory
                .Do( dbFactory =>
                {
                    builder
                        .RegisterInstance( dbProviderFactory )
                        .As<IDbProviderFactory>()
                        .IfNotRegistered( typeof(IDbProviderFactory) );
                } );
            builder
                .Register( ctx => ctx.Resolve<IDbProviderFactory>().Create() )
                .As<IDbProvider>()
                .ExternallyOwned()
                .OnlyIf( reg => 
                    reg.IsRegistered( new TypedService( typeof(IDbProviderFactory) ) ) &&
                    !reg.IsRegistered( new TypedService( typeof(IDbProvider) ) ) );
            builder
                .Register( ctx => ctx.Resolve<IDbProviderFactory>().DefaultDataBaseConfiguration )
                .As<IDataBaseConfiguration>()
                .ExternallyOwned()
                .OnlyIf( reg =>
                    reg.IsRegistered( new TypedService( typeof( IDbProviderFactory ) ) ) &&
                    !reg.IsRegistered( new TypedService( typeof( IDataBaseConfiguration ) ) ) );


            // register EventStoreSubscriberContext
            builder
                .RegisterType<EventStoreSubscriberContext>()
                .As<IEventStoreSubscriberContext>()
                .ExternallyOwned()
                .IfNotRegistered( typeof(IEventStoreSubscriberContext) );
            //builder.RegisterType<ILogger>()
            builder
                .RegisterType<IScheduler>() //
                .As<IScheduler>()
                .ExternallyOwned()
                .IfNotRegistered( typeof(IScheduler) );

            // register EventSubscribers
            var serviceSubscribers = service.GetEventSubscribers()
                .Select( subscriber => new { Type = subscriber.Key, Factory = subscriber.Value } )
                .ToList();
            var registeredSubscribers = serviceSubscribers.Select( s => s.Type ).ToArray();
            serviceSubscribers
                .ForEach( subscriver =>
                {
                    builder
                        .Register( ctx => Convert.ChangeType( subscriver.Factory(), subscriver.Type ) )
                        .As( subscriver.Type )
                        .As<IEventSubscriber>()
                        .ExternallyOwned()
                        .IfNotRegistered( subscriver.Type );
                } );

            // register service
            builder
                .Register( ctx =>
                {
                    serviceInitializer.Do( initializer => initializer( ctx, service ) );

                    // IEventStoreConfiguration
                    ctx.ResolveOptional<IEventStoreConfiguration>().Do( config => service.Configuration.Value = config );

                    // IDbProviderFactory
                    ctx.ResolveOptional<IDbProviderFactory>().Do( factory => service.SetDataBase( factory ) );

                    // Register event handlers
                    var cmdHandlers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( registration => 
                            registration.Activator.LimitType.IsAssignableTo<ICommandHandler>() &&
                            !registeredSubscribers.Contains( registration.Activator.LimitType ) ) // prevent cyclic registration
                        .Select( registration =>
                        {
                            var factoryGenerator = new FactoryGenerator( typeof(Func<ICommandHandler>), registration, ParameterMapping.ByType );
                            return (Func<ICommandHandler>) factoryGenerator.GenerateFactory( ctx, new Parameter[] { } );
                        } )
                        .Where( factory => factory != null )
                        .ToList();
                    cmdHandlers.ForEach( handler => service.RegisterCommandHandler( handler ) );

                    // Register event subscribers
                    var subscribers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( registration => registration.Activator.LimitType.IsAssignableTo<IEventSubscriber>() )
                        .Select( registration =>
                        {
                            var factoryGenerator = new FactoryGenerator( typeof( Func<IEventSubscriber> ), registration, ParameterMapping.ByType );
                            return (Func<IEventSubscriber>)factoryGenerator.GenerateFactory( ctx, new Parameter[] { } );
                        } )
                        .Where( factory => factory != null )
                        .ToList();
                    subscribers.ForEach( s => service.RegisterEventSubscriber( s ) );

                    return service.Initialize();
                } )
                .As<IEventStoreKitService>()
                .AutoActivate()
                .SingleInstance();
        }
    }
}