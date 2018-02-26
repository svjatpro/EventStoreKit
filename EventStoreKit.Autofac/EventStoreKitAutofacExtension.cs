using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Autofac.Features.GeneratedFactories;
using CommonDomain;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Logging;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;
using Module = Autofac.Module;

namespace EventStoreKit.Autofac
{
    public class EventStoreModule : Module
    {
        protected override void AttachToComponentRegistration( IComponentRegistry registry, IComponentRegistration registration )
        {
            registration.Preparing += ( sender, args ) =>
            {
                args.Parameters = args.Parameters.Union( new[]
                {
                    new ResolvedParameter( 
                        (p,c) => p.ParameterType.IsAssignableTo<ILogger>(),
                        (p,c) => c.Resolve<ILogger>( TypedParameter.From( args.Component.Activator.LimitType ) ) ),
                } );
            };
        }
    }

    public static class EventStoreKitAutofacExtension
    {
        public static void InitializeEventStoreKitService( this ContainerBuilder builder,
            Action<IComponentContext, IEventStoreKitServiceBuilder> serviceInitializer )
        {
            builder.InitializeEventStoreKitService( null, serviceInitializer );
        }

        public static void InitializeEventStoreKitService(
            this ContainerBuilder builder,
            Action<IEventStoreKitServiceBuilder> preInitialize = null,
            Action<IComponentContext, IEventStoreKitServiceBuilder> initialize = null )
        {
            builder.RegisterModule<EventStoreModule>();

            // create service instance
            var service = new EventStoreKitService( false );
            preInitialize.Do( creator => creator( service ) );

            // register default EventStoreConfiguration
            builder.RegisterInstance( service.Configuration.GetValueOrDefault() )
                .As<IEventStoreConfiguration>()
                .IfNotRegistered( typeof(IEventStoreConfiguration) )
                .SingleInstance();

            // register default LoggerFactory
            builder.RegisterInstance( service.LoggerFactory.GetValueOrDefault() )
                .As<ILoggerFactory>()
                .IfNotRegistered( typeof( ILoggerFactory ) )
                .ExternallyOwned();
            builder
                .Register( ( ctx, p ) => typeof( ILoggerFactory )
                    .GetMethods( BindingFlags.Public | BindingFlags.Instance )
                    .First( m => m.Name == "Create" && m.IsGenericMethod )
                    .MakeGenericMethod( p.TypedAs<Type>() )
                    .Invoke( ctx.Resolve<ILoggerFactory>(), new object[] { } ) )
                .OnPreparing( args => args.Parameters = args.Parameters.Union( new[] { TypedParameter.From( typeof( EventStoreKitService ) ) } ) )
                .As<ILogger>()
                .IfNotRegistered( typeof( ILogger ) )
                .ExternallyOwned();

            // register default scheduler
            builder
                .RegisterInstance( service.Scheduler.GetValueOrDefault() )
                .As<IScheduler>()
                .IfNotRegistered( typeof( IScheduler ) )
                .SingleInstance();

            // register default IDbProviderFactory and IDataBaseConfiguration
            builder
                .RegisterInstance( service.DbProviderFactorySubscriber.GetValueOrDefault() )
                .As<IDbProviderFactory>()
                .IfNotRegistered( typeof( IDbProviderFactory ) );
            builder
                .Register( ctx => ctx.Resolve<IDbProviderFactory>().Create() )
                .As<IDbProvider>()
                .ExternallyOwned()
                .OnlyIf( reg =>
                    reg.IsRegistered( new TypedService( typeof( IDbProviderFactory ) ) ) &&
                    !reg.IsRegistered( new TypedService( typeof( IDbProvider ) ) ) );
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
                    initialize.Do( initializer => initializer( ctx, service ) );

                    // IEventStoreConfiguration
                    ctx.ResolveOptional<IEventStoreConfiguration>().Do( config => service.Configuration.Value = config );

                    // ILoggerFactory
                    ctx.ResolveOptional<ILoggerFactory>().Do( log => service.LoggerFactory.Value = log );

                    // IScheduler
                    ctx.ResolveOptional<IScheduler>().Do( scheduler => service.Scheduler.Value = scheduler );

                    // IDbProviderFactory
                    ctx.ResolveOptional<IDbProviderFactory>().Do( factory => service.DbProviderFactorySubscriber.Value = factory );

                    // Register aggregates command handlers
                    var cmdAggregatesHandlers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( registration => 
                            registration.Activator.LimitType.IsAssignableTo<IAggregate>() &&
                            registration.Activator.LimitType.GetInterfaces().Any( i =>
                                i.IsGenericType && i.GetGenericTypeDefinition() == typeof( ICommandHandler<> ).GetGenericTypeDefinition() ) )
                        .Select( registration => registration.Activator.LimitType )
                        .ToList();
                    cmdAggregatesHandlers.ForEach( type => service.RegisterAggregateCommandHandler( type ) );

                    // Register command handlers
                    var cmdHandlers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( registration => registration.Activator.LimitType.GetInterfaces().Any( i => 
                            i.IsGenericType && i.GetGenericTypeDefinition() == typeof( ICommandHandler<,> ).GetGenericTypeDefinition() ) )
                        .Select( registration =>
                        {
                            var factoryGenerator = new FactoryGenerator( typeof( Func<ICommandHandler> ), registration, ParameterMapping.ByType );
                            return (Func<ICommandHandler>)factoryGenerator.GenerateFactory( ctx, new Parameter[] { } );
                        } )
                        .Where( factory => factory != null )
                        .ToList();
                    cmdHandlers.ForEach( handler => service.RegisterCommandHandler( handler ) );

                    // Register event subscribers
                    var subscribers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( registration => 
                            registration.Activator.LimitType.IsAssignableTo<IEventSubscriber>() &&
                            !registeredSubscribers.Contains( registration.Activator.LimitType ) ) // prevent cyclic registration
                        .Select( registration =>
                        {
                            var factoryGenerator = new FactoryGenerator( typeof( Func<IEventSubscriber> ), registration, ParameterMapping.ByType );
                            return (Func<IEventSubscriber>)factoryGenerator.GenerateFactory( ctx, new Parameter[] { } );
                        } )
                        .Where( factory => factory != null )
                        .ToList();
                    subscribers.ForEach( s => service.RegisterEventSubscriber( s ) );

                    service.Initialize();
                    return service;
                } )
                .As<IEventStoreKitService>()
                //.As<ICommandBus>()
                //.As<IEventPublisher>()
                .AutoActivate()
                .SingleInstance()
                .OwnedByLifetimeScope();
        }
    }
}