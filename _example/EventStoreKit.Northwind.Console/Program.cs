using System;
using System.Reflection;
using Autofac;
using EventStoreKit.CommandBus;
using EventStoreKit.Example;
using EventStoreKit.linq2db;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Services;

namespace EventStoreKit.Northwind.Console
{
    class Program
    {
        static void Main( string[] args )
        {
            const string dbConfig = "NorthwindDb";
            log4net.Config.XmlConfigurator.Configure();

            var builder = new ContainerBuilder();
            builder.RegisterModule( new EventStoreModule( DbProviderFactory.SqlDialectType( dbConfig ), configurationString: dbConfig ) );
            builder.RegisterModule( new NorthwindModule() );
            builder.RegisterType<CurrentUserProviderStub>().As<ICurrentUserProvider>().SingleInstance();
            var container = builder.Build();
            
            var commandBus = container.Resolve<ICommandBus>();

            commandBus.Send( new CreateCustomerCommand
            {
                Id = Guid.NewGuid(),
                CompanyName = "company1",
                ContactName = "contact1",
                ContactTitle = "contacttitle1",
                ContactPhone = "contactphone",
                Address = "address",
                City = "city",
                Country = "country",
                Region = "region",
                PostalCode = "zip"
            } );
        }
    }
}
