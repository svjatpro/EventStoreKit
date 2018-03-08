using System;
using System.Threading.Tasks;
using EventStoreKit.DbProviders;
using EventStoreKit.Services;
using Northwind.Embedded.Console;

namespace EventStoreKit.Embedded.Northwind.Console
{
    static class Program
    {
        static void Main()
        {
            var service = new NorthwindService( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, "data source=northwind.db" ) );
            service.CleanData();

            SyncExample( service );
            //AsyncExample( service ); // the async example doesn't work in SQLite because of SQLite lock constraint, to run this example please switch to another available DataBase
        }

        static void SyncExample( NorthwindService service )
        {
            // create customer
            var customer1 = service.CreateCustomer( "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" );
            System.Console.WriteLine( customer1.CompanyName );

            // create products
            var prod1 = service.CreateProduct( "product1", 12.3m );
            var prod2 = service.CreateProduct( "product2", 23.4m );
            var prod3 = service.CreateProduct( "product3", 34.5m );
            service.GetProducts().ForEach( p => System.Console.WriteLine( p.ProductName ) );

            // create order
            var order1 = service.CreateOrder( customer1.Id, DateTime.Now, DateTime.Now.AddDays( 1 ) );
            service.GetOrders().ForEach( o => System.Console.WriteLine( "order: date = {0}; customer = {1}", o.RequiredDate.ToShortDateString(), o.CustomerName ) );

            // create order details
            service.CreateOrderDetail( order1.Id, prod1.Id, 123, 2 );
            service.CreateOrderDetail( order1.Id, prod2.Id, 234, 3 );
            service.GetOrderDetails( order1.Id ).ForEach( o => System.Console.WriteLine( "detail: product = {0}; price = {1}", o.ProductName, o.UnitPrice ) );
        }

        static void AsyncExample( NorthwindService service )
        {
            var customerId1 = Guid.NewGuid();
            var prodId1 = Guid.NewGuid();
            var prodId2 = Guid.NewGuid();
            var prodId3 = Guid.NewGuid();
            var orderId1 = Guid.NewGuid();
            var detailId1 = Guid.NewGuid();
            var detailId2 = Guid.NewGuid();
            
            Task.WaitAll( 
                service.CreateCustomerAsync( customerId1, "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" ),
                service.CreateProductAsync( prodId1, "product1", 12.3m ), 
                service.CreateProductAsync( prodId2, "product2", 23.4m ), 
                service.CreateProductAsync( prodId3, "product3", 34.5m ),
                service.CreateOrderAsync( orderId1, customerId1, DateTime.Now, DateTime.Now.AddDays( 1 ) ),
                service.CreateOrderDetailAsync( detailId1, orderId1, prodId1, 123, 2 ),
                service.CreateOrderDetailAsync( detailId2, orderId1, prodId2, 234, 3 ) );

            service.GetCustomers().ForEach( customer => System.Console.WriteLine( customer.CompanyName ) );
            service.GetProducts().ForEach( p => System.Console.WriteLine( p.ProductName ) );
            service.GetOrders().ForEach( o => System.Console.WriteLine( "order: date = {0}; customer = {1}", o.RequiredDate.ToShortDateString(), o.CustomerName ) );
            service.GetOrderDetails( orderId1 ).ForEach( o => System.Console.WriteLine( "detail: product = {0}; price = {1}", o.ProductName, o.UnitPrice ) );
        }
    }
}
