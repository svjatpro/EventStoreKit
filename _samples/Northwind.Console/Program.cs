using System;
using System.Linq;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.DbProviders;
using EventStoreKit.linq2db;
using EventStoreKit.Messages;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Northwind.Projections.Customer;
using EventStoreKit.Northwind.Projections.OrderDetail;
using EventStoreKit.Projections;
using EventStoreKit.Services;

namespace EventStoreKit.Northwind.Console
{
    //public class NorthwindModule : Module
    //{
    //    protected override void Load( ContainerBuilder builder )
    //    {
    //        // register command handlers
    //        builder.RegisterType<CustomerHandler>()
    //            .As<ICommandHandler<CreateCustomerCommand, Customer>>()
    //            .As<ICommandHandler<UpdateCustomerCommand, Customer>>();
    //        builder.RegisterType<ProductHandler>().AsImplementedInterfaces();
    //        builder.RegisterType<OrderHandler>().AsImplementedInterfaces().SingleInstance();
    //        builder.RegisterType<OrderDetailHandler>().AsImplementedInterfaces();

    //        // configure data base
    //        builder
    //            .RegisterInstance( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, "data source=db1" ) )
    //            .As<IDataBaseConfiguration>()
    //            .SingleInstance();
    //        builder
    //            .RegisterType<Linq2DbProviderFactory>()
    //            .As<IDbProviderFactory>()
    //            .SingleInstance();

    //        // register projections
    //        builder.RegisterType<ProductProjection>().SingleInstance();
    //        builder.RegisterType<CustomerProjection>().SingleInstance();
    //        builder.RegisterType<OrderProjection>().SingleInstance();
    //        builder.RegisterType<OrderDetailProjection>().SingleInstance();
    //    }
    //}

    //var builder = new ContainerBuilder();
    //builder.RegisterModule<NorthwindModule>();
    //builder.InitializeEventStoreKitService();

    //var container = builder.Build();

    //var service = container.Resolve<IEventStoreKitService>();


    static class Program
    {
        #region Private methods

        private static void SyncProcessCommand<TSubscriber,TEvent>( this IEventStoreKitService service, DomainCommand cmd )
            where TSubscriber: IEventSubscriber
            where TEvent : DomainEvent
        {
            var subscriber = service.GetSubscriber<TSubscriber>();
            var messageHandled = subscriber.When<TEvent>( msg => msg.Id == cmd.Id );
            service.SendCommand( cmd );
            messageHandled.Wait( 1000 );
        }
        private static Guid CreateCustomer(
            this IEventStoreKitService service,
            string companyName,
            string contactName, string contactTitle, string contactPhone,
            string address, string city, string region, string country, string postalCode)
        {
            var id = Guid.NewGuid();
            var cmd = new CreateCustomerCommand
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
            };
            service.SyncProcessCommand<CustomerProjection, CustomerCreatedEvent>( cmd );
            return id;
        }

        private static Guid CreateProduct( this IEventStoreKitService service, string name, decimal price )
        {
            var id = Guid.NewGuid();
            var cmd = new CreateProductCommand
            {
                Id = id,
                ProductName = name,
                UnitPrice = price
            };
            service.SyncProcessCommand<ProductProjection, ProductCreatedEvent>( cmd );
            return id;
        }

        private static Guid CreateOrder( this IEventStoreKitService service, Guid customerId, DateTime orderDate, DateTime requireDate )
        {
            var id = Guid.NewGuid();
            var cmd = new CreateOrderCommand
            {
                Id = id,
                CustomerId = customerId,
                OrderDate = orderDate,
                RequiredDate = requireDate
            };
            service.SyncProcessCommand<OrderProjection, OrderCreatedEvent>( cmd );
            return id;
        }

        private static Guid CreateOrderDetail( this IEventStoreKitService service, Guid orderId, Guid productId,
            decimal unitPrice, decimal quantity, decimal discount = 0 )
        {
            var id = Guid.NewGuid();
            var cmd = new CreateOrderDetailCommand
            {
                Id = id,
                OrderId = orderId,
                ProductId = productId,
                UnitPrice = unitPrice,
                Quantity = quantity,
                Discount = discount
            };
            service.SyncProcessCommand<OrderDetailProjection, OrderDetailCreatedEvent>( cmd );
            return id;
        }

        #endregion

        static void Main()
        {
            var service = new EventStoreKitService( false )
                .SetDataBase<Linq2DbProviderFactory>( new DataBaseConfiguration( DataBaseConnectionType.SqlLite, "data source=northwind.db" ) )
                .RegisterAggregate<Product>()
                .RegisterAggregate<Customer>()
                .RegisterAggregate<Order>()
                .RegisterAggregate<OrderDetail>()
                .RegisterEventSubscriber<ProductProjection>()
                .RegisterEventSubscriber<CustomerProjection>()
                .RegisterEventSubscriber<OrderProjection>()
                .RegisterEventSubscriber<OrderDetailProjection>()
                .Initialize();

            service.CleanData();

            // create customer
            var customerId1 = service.CreateCustomer( "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" );
            service.GetSubscriber<CustomerProjection>()
                .GetCustomers( null ).ToList()
                .ForEach( c => System.Console.WriteLine( c.CompanyName ) );

            // create products
            var prod1 = service.CreateProduct( "product1", 12.3m );
            var prod2 = service.CreateProduct( "product2", 23.4m );
            var prod3 = service.CreateProduct( "product3", 34.5m );
            service.GetSubscriber<ProductProjection>()
                .GetProducts( null ).ToList()
                .ForEach( p => System.Console.WriteLine( p.ProductName ) );

            // create order
            var orderId1 = service.CreateOrder( customerId1, DateTime.Now, DateTime.Now.AddDays( 1 ) );
            service.GetSubscriber<OrderProjection>()
                .GetOrders( null ).ToList()
                .ForEach( o => System.Console.WriteLine( "order: date = {0}; customer = {1}", o.RequiredDate.ToShortDateString(), o.CustomerName ) );

            // create order details
            service.CreateOrderDetail( orderId1, prod1, 123, 2 );
            service.CreateOrderDetail( orderId1, prod2, 234, 3 );
            service.GetSubscriber<OrderDetailProjection>()
                .GetOrderDetails( orderId1 ).ToList()
                .ForEach( o => System.Console.WriteLine( "detail: product = {0}; price = {1}", o.ProductName, o.UnitPrice ) );
        }
    }
}
