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
            base.Load( builder );

            // Infrasctructure classes, which are not registered by default                        
            builder
                .Register( context => new DbProviderFactory( "NorthwindDb" ) ) // before start, validate 'NorthwindDb' connection string in *.config
                //.Register( context => new DbProviderFactory( SqlClientType.MsSqlClient, "Server=localhost;Initial Catalog=NorthwindEventStore;Integrated Security=True" ) ) // alternative way
                .As<IDbProviderFactory>()
                .SingleInstance();
            builder
                .Register( context => context.Resolve<IDbProviderFactory>().CreateProjectionProvider() )
                .As<IDbProvider>()
                .ExternallyOwned();

            builder.RegisterGeneric( typeof( Logger<> ) ).As( typeof( ILogger<> ) );

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

            //// Services
            ////builder.RegisterType<SecurityManagerStub>().As<ISecurityManager>();
            //builder.RegisterType<ReportService>().As<IReportService>();

            //builder.RegisterType<DataSourceText>().As<IDataSource>();
            //builder.RegisterType<DataSourceHtml>().As<IDataSource>();
            //builder.RegisterType<DataImporter>().AsSelf();
            //builder.RegisterType<PaymentImporter>().As<IPaymentImporter>();

            //builder.RegisterType<OsbbConfiguration>()
            //    .As<IOsbbConfiguration>()
            //    .As<IEventStoreConfiguration>();
        }
    }
}
