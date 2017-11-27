using System.Data.Common;
using System.Reflection;
using EventStoreKit.DbProviders;
using EventStoreKit.Logging;
using EventStoreKit.Northwind.AggregatesHandlers;

namespace EventStoreKit.Example
{
    //public class NorthwindModule : Module
    //{
    //    protected override void Load( ContainerBuilder builder )
    //    {
    //        // Infrasctructure classes, which are not registered by default                        
    //        builder.RegisterGeneric( typeof( Logger<> ) ).As( typeof( ILogger<> ) ); // log4net logger
    //        builder
    //            .Register( context => new DbProviderFactory( "NorthwindDb" ) ) // before start, validate 'NorthwindDb' connection string in *.config
    //            //.Register( context => new DbProviderFactory( "NorthwindDb" ) ) // before start, validate 'NorthwindDb' connection string in *.config
    //            //.Register( context => new DbProviderFactory( SqlClientType.MsSqlClient, "Server=localhost;Initial Catalog=NorthwindEventStore;Integrated Security=True" ) ) // alternative way
    //            .As<IDbProviderFactory>()
    //            .SingleInstance();
            
    //        // handlers for Aggregates
    //        builder.RegisterType<CustomerHandler>().AsImplementedInterfaces().SingleInstance();
            
    //        //// handlers for Sagas
    //        //builder.RegisterType<PremisesRentCalculationSagaHandlers>().As<IEventSubscriber>().SingleInstance();
    //        //builder.RegisterType<PremisesTenantSagaHandlers>().As<IEventSubscriber>().SingleInstance();
    //        //builder.RegisterType<ReportPeriodSagaHandlers>().As<IEventSubscriber>().SingleInstance();
    //        //builder.RegisterType<ReportPeriodActiveSagaHandlers>().As<IEventSubscriber>().SingleInstance();
    //        //builder.RegisterType<PaymentResolveSagaHandlers>().As<IEventSubscriber>().SingleInstance();
    //        //builder.RegisterType<PaymentContainerSagaHandlers>().As<IEventSubscriber>().SingleInstance();
    //        //builder.RegisterType<OrganizationPremisesSagaHandlers>().As<IEventSubscriber>().SingleInstance();

    //        // Projections
    //        //builder.RegisterType<OsbbUserProjection>().AsImplementedInterfaces().SingleInstance();
    //    }
    //}
}
