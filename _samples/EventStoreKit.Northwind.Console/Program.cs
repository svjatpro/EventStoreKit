using System;
using System.Linq;
using EventStoreKit.DbProviders;
using EventStoreKit.linq2db;
using EventStoreKit.Messages;
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

        private static Guid CreateOrder( this IEventStoreKitService service, Guid customerId, DateTime orderDate, DateTime requireDate )
        {
            var id = Guid.NewGuid();
            service.SendCommand( new CreateOrderCommand
            {
                Id = id,
                CustomerId = customerId,
                OrderDate = orderDate,
                RequiredDate = requireDate
            } );
            return id;
        }

        #endregion

        static void Main()
        {
            var service = new EventStoreKitService()
                .RegisterCommandHandler<CustomerHandler>()
                .RegisterCommandHandler<ProductHandler>()
                .RegisterCommandHandler<OrderHandler>()
                .SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
                .SetSubscriberDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
                .RegisterEventSubscriber<ProductProjection>()
                .RegisterEventSubscriber<CustomerProjection>()
                .RegisterEventSubscriber<OrderProjection>();

            var customerProjection = service.ResolveSubscriber<CustomerProjection>();
            var productProjection = service.ResolveSubscriber<ProductProjection>();
            var orderProjection = service.ResolveSubscriber<OrderProjection>();

            var customerId1 = service.CreateCustomer( "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" );
            service.CreateProduct( "product1", 12.3m );
            service.CreateProduct( "product2", 23.4m );
            service.CreateProduct( "product3", 34.5m );
            var orderId1 = service.CreateOrder( customerId1, DateTime.Now, DateTime.Now.AddDays( 1 ) );

            customerProjection.WaitMessages();
            productProjection.WaitMessages();
            orderProjection.WaitMessages();

            customerProjection.GetCustomers( null ).ToList().ForEach( c => System.Console.WriteLine( c.CompanyName ) );
            productProjection.GetProducts( null ).ToList().ForEach( p => System.Console.WriteLine( p.ProductName ) );
            orderProjection.GetOrders( null ).ToList().ForEach( o => System.Console.WriteLine( "order: date = {0}; customer = {1}", o.RequiredDate.ToShortDateString(), o.CustomerName ) );
        }
    }
}
