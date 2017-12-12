using System;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using EventStoreKit.DbProviders;
using EventStoreKit.linq2db;
using EventStoreKit.Northwind.AggregatesHandlers;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Projections.Customer;
using EventStoreKit.Services;
using OSMD.Common.ReadModels;

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
            //var db1 = new DbProviderSqlLite( null, "data source=db1" );
            //var db2 = new DbProviderSqlLite( null, "data source=db2" );

            //db1.CreateTable<ProductModel>();
            //db1.Insert( new ProductModel {Id = Guid.NewGuid(), ProductName = "p1", UnitPrice = 12} );
            //db2.CreateTable<CustomerModel>();
            //db2.Insert( new CustomerModel { Id = Guid.NewGuid(), Address = "", City = "", CompanyName = "", ContactName = "", ContactPhone = "", ContactTitle = "", Country = "", PostalCode = "", Region = "" } );

            var service = new EventStoreKitService()
                //.RegisterDbProviderFactory<Linq2DbProviderFactory>("NorthwindSqlLite")
                //.MapEventStoreDb<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db2" )
                .RegisterDbProviderFactory<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
                
                //.MapReadModelDb<ProductModel>( DbConnectionType.SqlLite, "data source=db3" )

                .RegisterCommandHandler<CustomerHandler>()
                .RegisterCommandHandler<ProductHandler>()

                .RegisterEventSubscriber( ctx => new CustomerProjection( ctx ), DbConnectionType.SqlLite, "data source=db2" )
                .RegisterEventSubscriber( ctx => new ProductProjection( ctx ) );

            var customerProjection = service.ResolveSubscriber<CustomerProjection>();
            var productProjection = service.ResolveSubscriber<ProductProjection>();

            service.CreateCustomer( "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" );
            service.CreateProduct( "product1", 12.3m );
            service.CreateProduct( "product2", 23.4m );
            service.CreateProduct( "product3", 34.5m );

            customerProjection.WaitMessages();
            productProjection.WaitMessages();

            //new Func<IDbProvider>( () => service.ResolveDbProviderFactory<ProductModel>().Create() )
            //    .Run( db => db.Query<ProductModel>().ToList() )
            //    .ForEach( p => System.Console.WriteLine( p.ProductName ) );

            new Func<IDbProvider>( () => service.ResolveDbProviderFactory<Commits>().Create() )
                .Run( db => db.Query<Commits>().ToList() )
                .ForEach( c => System.Console.WriteLine( c.CheckpointNumber ) );

            customerProjection.GetCustomers( null ).ToList().ForEach( c => System.Console.WriteLine( c.CompanyName ) );
            productProjection.GetProducts( null ).ToList().ForEach( p => System.Console.WriteLine( p.ProductName ) );
        }
    }
}
