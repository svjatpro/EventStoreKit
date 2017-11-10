using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;
using EventStoreKit.DbProviders;
using EventStoreKit.linq2db;
using EventStoreKit.log4net;
using EventStoreKit.Logging;
using EventStoreKit.Northwind.AggregatesHandlers;
using EventStoreKit.Projections;
using EventStoreKit.Services.Configuration;

namespace EventStoreKit.Example
{
    public class NorthwindModule : Module
    {
        protected override void Load( ContainerBuilder builder )
        {
            // Infrasctructure classes, which are not registered by default                        
            builder.RegisterGeneric( typeof( Logger<> ) ).As( typeof( ILogger<> ) ); // log4net logger
            builder
                .Register( context => new DbProviderFactory( "NorthwindDb" ) ) // before start, validate 'NorthwindDb' connection string in *.config
                //.Register( context => new DbProviderFactory( "NorthwindDb" ) ) // before start, validate 'NorthwindDb' connection string in *.config
                //.Register( context => new DbProviderFactory( SqlClientType.MsSqlClient, "Server=localhost;Initial Catalog=NorthwindEventStore;Integrated Security=True" ) ) // alternative way
                .As<IDbProviderFactory>()
                .SingleInstance();
            
            // handlers for Aggregates
            builder.RegisterType<CustomerHandler>().AsImplementedInterfaces().SingleInstance();
            
            //// handlers for Sagas
            //builder.RegisterType<PremisesRentCalculationSagaHandlers>().As<IEventSubscriber>().SingleInstance();
            //builder.RegisterType<PremisesTenantSagaHandlers>().As<IEventSubscriber>().SingleInstance();
            //builder.RegisterType<ReportPeriodSagaHandlers>().As<IEventSubscriber>().SingleInstance();
            //builder.RegisterType<ReportPeriodActiveSagaHandlers>().As<IEventSubscriber>().SingleInstance();
            //builder.RegisterType<PaymentResolveSagaHandlers>().As<IEventSubscriber>().SingleInstance();
            //builder.RegisterType<PaymentContainerSagaHandlers>().As<IEventSubscriber>().SingleInstance();
            //builder.RegisterType<OrganizationPremisesSagaHandlers>().As<IEventSubscriber>().SingleInstance();

            // Projections
            //builder.RegisterType<OsbbUserProjection>().AsImplementedInterfaces().SingleInstance();
        }
    }
}
