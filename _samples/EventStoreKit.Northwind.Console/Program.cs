using System;
using System.Linq;
using EventStoreKit.Northwind.AggregatesHandlers;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Projections.Customer;
using EventStoreKit.Services;

namespace EventStoreKit.Northwind.Console
{
    static class Program
    {
        #region Private methods

        private static Guid CreateCustomer(
            this IEventStoreKitService service,
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

        private static Guid CreateProduct( this IEventStoreKitService service, string name, decimal price )
        {
            var id = Guid.NewGuid();
            service.SendCommand(new CreateProductCommand
            {
                Id = id,
                ProductName = name,
                UnitPrice = price
            });
            return id;
        }

        #endregion

        static void Main( string[] args )
        {
            var service = new EventStoreKitService()
                .RegisterDbProviderFactory<IDbProviderLinq2Db>( configurationString )
                .RegisterDbProviderFactory<IDbProviderLinq2Db>( configurationString )

                .RegisterCommandHandler<CustomerHandler>()
                .RegisterCommandHandler<ProductHandler>()
 
                .RegisterEventSubscriber<CustomerProjection>()
                .RegisterEventSubscriber<ProductProjection>();

            var customerProjection = service.ResolveSubscriber<CustomerProjection>();
            var productProjection = service.ResolveSubscriber<ProductProjection>();
            
            service.CreateCustomer( "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" );
            service.CreateProduct( "product1", 12.3m );
            service.CreateProduct( "product2", 23.4m );
            service.CreateProduct( "product3", 34.5m );

            customerProjection.WaitMessages();
            productProjection.WaitMessages();

            customerProjection.GetCustomers( null ).ToList().ForEach( c => System.Console.WriteLine( c.CompanyName ) );
            productProjection.GetProducts( null ).ToList().ForEach( p => System.Console.WriteLine( p.ProductName ) );
        }
    }
}
