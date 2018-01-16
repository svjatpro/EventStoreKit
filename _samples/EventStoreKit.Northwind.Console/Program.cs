using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Autofac.Core;
using Autofac.Core.Activators.Reflection;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.linq2db;
using EventStoreKit.Messages;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.AggregatesHandlers;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Projections.Customer;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using LinqToDB.Extensions;

namespace EventStoreKit.Northwind.Console
{

    public class NorthwindModule : Module
    {
        //private class Startup : IStartable
        //{
        //    public Startup()
        //    {

        //    }

        //    public void Start()
        //    {

        //    }
        //}

        protected override void Load( ContainerBuilder builder )
        {
            //base.Load( builder );
            //builder.RegisterType<Startup>().As<IStartable>();

            //builder.RegisterType<CustomerHandler>()
            //    .As<ICommandHandler<CreateCustomerCommand, Customer>>()
            //    .As<ICommandHandler<UpdateCustomerCommand, Customer>>();
            //builder.RegisterType<ProductHandler>().AsImplementedInterfaces();
            //builder.RegisterType<OrderHandler>().AsImplementedInterfaces();
            //builder.RegisterType<OrderDetailHandler>().AsImplementedInterfaces();


        }
    }

    public static class EventStoreKitExtension
    {
        public static void InitializeEventStoreKitService( 
            this ContainerBuilder builder,
            Action<EventStoreKitService,IComponentContext> initializer = null )
        {
            builder
                .Register( ctx =>
                {
                    var service = new EventStoreKitService();

                    // Register event handlers
                    var cmdHandlers = ctx.ComponentRegistry
                        .Registrations
                        .Where( r => r.Activator.LimitType.IsAssignableTo<ICommandHandler>() )
                        .Select( r =>
                            ctx.IsRegistered( r.Activator.LimitType ) ?
                            ctx.Resolve( r.Activator.LimitType ) :
                            r.Services.FirstOrDefault().With( ctx.ResolveService ) )
                        .Select( h => h.OfType<ICommandHandler>() )
                        .Where( h => h != null )
                        .ToList();
                    cmdHandlers.ForEach( handler => service.RegisterCommandHandler( handler ) );

                    // Register event subscribers

                    initializer.Do( initialize => initialize( service, ctx ) );
                    return service;
                } )
                .As<IEventStoreKitService>()
                .AutoActivate()
                .SingleInstance();
        }
    }

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

        private static Guid CreateOrderDetail( this IEventStoreKitService service, Guid orderId, Guid productId,
            decimal unitPrice, decimal quantity, decimal discount = 0 )
        {
            var id = Guid.NewGuid();
            service.SendCommand( new CreateOrderDetailCommand
            {
                Id = id,
                OrderId = orderId,
                ProductId = productId,
                UnitPrice = unitPrice,
                Quantity = quantity,
                Discount = discount
            } );
            return id;
        }

        #endregion

        static void Main()
        {
            //var builder = new ContainerBuilder();
            //builder.RegisterModule<NorthwindModule>();

            //builder.InitializeEventStoreKitService( ( srv, ctx ) =>
            //{
            //    srv
            //            .RegisterCommandHandler<CustomerHandler>()
            //            .RegisterCommandHandler<ProductHandler>()
            //            .RegisterCommandHandler<OrderHandler>()
            //            .RegisterCommandHandler<OrderDetailHandler>()
            //        .SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
            //        .SetSubscriberDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
            //        .RegisterEventSubscriber<ProductProjection>()
            //        .RegisterEventSubscriber<CustomerProjection>()
            //        .RegisterEventSubscriber<OrderProjection>()
            //        .RegisterEventSubscriber<OrderDetailProjection>();
            //} );
            //var container = builder.Build();

            //var service = container.Resolve<IEventStoreKitService>();
            // ----------------------------------------------

            var service = new EventStoreKitService()
                .RegisterCommandHandler<CustomerHandler>()
                .RegisterCommandHandler<ProductHandler>()
                .RegisterCommandHandler<OrderHandler>()
                .RegisterCommandHandler<OrderDetailHandler>()
                .SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
                .SetSubscriberDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
                .RegisterEventSubscriber<ProductProjection>()
                .RegisterEventSubscriber<CustomerProjection>()
                .RegisterEventSubscriber<OrderProjection>()
                //.RegisterEventSubscriber<OrderDetailProjection>()
                ;

            var customerProjection = service.ResolveSubscriber<CustomerProjection>();
            var productProjection = service.ResolveSubscriber<ProductProjection>();
            var orderProjection = service.ResolveSubscriber<OrderProjection>();
            //var orderDetailProjection = service.ResolveSubscriber<OrderDetailProjection>();

            var customerId1 = service.CreateCustomer( "company1", "contact1", "contacttitle1", "contactphone", "address", "city", "country", "region", "zip" );
            var prod1 = service.CreateProduct( "product1", 12.3m );
            var prod2 = service.CreateProduct( "product2", 23.4m );
            var prod3 = service.CreateProduct( "product3", 34.5m );
            var orderId1 = service.CreateOrder( customerId1, DateTime.Now, DateTime.Now.AddDays( 1 ) );
            //service.CreateOrderDetail( orderId1, prod1, 123, 2 );
            //service.CreateOrderDetail( orderId1, prod2, 234, 3 );

            customerProjection.WaitMessages();
            productProjection.WaitMessages();
            orderProjection.WaitMessages();
            //orderDetailProjection.WaitMessages();

            customerProjection.GetCustomers( null ).ToList().ForEach( c => System.Console.WriteLine( c.CompanyName ) );
            productProjection.GetProducts( null ).ToList().ForEach( p => System.Console.WriteLine( p.ProductName ) );
            orderProjection.GetOrders( null ).ToList().ForEach( o => System.Console.WriteLine( "order: date = {0}; customer = {1}", o.RequiredDate.ToShortDateString(), o.CustomerName ) );
            //orderDetailProjection.GetOrderDetails( orderId1 ).ToList().ForEach( o => System.Console.WriteLine( "detail: product = {0}; price = {1}", o.ProductName, o.UnitPrice ) );
        }
    }
}
