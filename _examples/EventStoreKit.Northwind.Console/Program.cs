using System;
using System.Reflection;
using Autofac;
using EventStoreKit.CommandBus;
using EventStoreKit.DbProviders;
using EventStoreKit.Example;
using EventStoreKit.linq2db;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Services;

namespace EventStoreKit.Northwind.Console
{
    class Program
    {
        static Guid CreateCustomer( 
            ICommandBus bus, 
            string companyName, 
            string contactName, string contactTitle, string contactPhone, 
            string address, string city, string region, string country, string postalCode )
        {
            var id = Guid.NewGuid();
            bus.Send( new CreateCustomerCommand
            {
                Id = id,
                CompanyName = companyName,
                ContactName = contactName,
                ContactTitle = contactTitle,
                ContactPhone = contactPhone,
                Address = address,
                City = city,
                Country = country,
                Region = region,
                PostalCode = postalCode
            } );
            return id;
        }

        static void Main( string[] args )
        {
            const string dbConfig = "NorthwindDb";
            log4net.Config.XmlConfigurator.Configure();

            var builder = new ContainerBuilder();
            builder.RegisterModule( new EventStoreModule( DbProviderFactory.SqlDialectType( dbConfig ), configurationString: dbConfig ) );
            builder.RegisterModule( new NorthwindModule() );
            var container = builder.Build();
            
            var commandBus = container.Resolve<ICommandBus>();

            CreateCustomer( commandBus, "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" );


        }
    }
}
