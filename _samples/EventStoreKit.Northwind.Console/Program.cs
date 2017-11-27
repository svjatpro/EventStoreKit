using System;
using System.Linq;
using EventStoreKit.CommandBus;
using EventStoreKit.Northwind.AggregatesHandlers;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Services;
using OSMD.Projections.Projections;

namespace EventStoreKit.Northwind.Console
{
    class Program
    {
        static Guid CreateCustomer(
            IEventStoreKitService service,
            string companyName,
            string contactName, string contactTitle, string contactPhone,
            string address, string city, string region, string country, string postalCode)
        {
            var id = Guid.NewGuid();
            service.SendCommand( new CreateCustomerCommand
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
            });
            return id;
        }

        static void Main( string[] args )
        {
            var service = new EventStoreKitService()
                .RegisterCommandHandler<CustomerHandler>()
                .RegisterCommandHandler<ProductHandler>()
                .RegisterEventSubscriber<CustomerProjection>();

            var customerProjection = service.ResolveSubscriber<CustomerProjection>();
            
            CreateCustomer( service, "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" );

            customerProjection.WaitMessages();

            customerProjection
                .GetCustomers( null )
                .ToList()
                .ForEach( c => System.Console.WriteLine( c.CompanyName ) );
        }
    }
}
