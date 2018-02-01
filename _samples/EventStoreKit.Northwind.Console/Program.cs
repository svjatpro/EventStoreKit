using Autofac;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Autofac.Features.GeneratedFactories;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.linq2db;
using EventStoreKit.Messages;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.AggregatesHandlers;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Northwind.Projections.Customer;
using EventStoreKit.Northwind.Projections.OrderDetail;
using EventStoreKit.Projections;
using EventStoreKit.Services;
using EventStoreKit.Services.Configuration;
using EventStoreKit.Utility;
using LinqToDB.DataProvider;
using Module = Autofac.Module;

namespace EventStoreKit.Northwind.Console
{

    public class NorthwindModule : Module
    {
        protected override void Load( ContainerBuilder builder )
        {
            builder.RegisterType<CustomerHandler>()
                .As<ICommandHandler<CreateCustomerCommand, Customer>>()
                .As<ICommandHandler<UpdateCustomerCommand, Customer>>();
            builder.RegisterType<ProductHandler>().AsImplementedInterfaces();
            builder.RegisterType<OrderHandler>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<OrderDetailHandler>().AsImplementedInterfaces();

            // register DbProviderFactory
            // builder.RegisterType<Linq2DbProviderFactory>().As<IDbProviderFactory>(); // can be used with .Create( configuration ) only
            // builder.Register( ctx => return new Linq2DbProviderFactory( defaultConfiguration ) ).As<IDbProviderFactory>();
            builder.RegisterType<Linq2DbProviderFactory>().As<IDbProviderFactory>();


        }
    }
    

    public static class EventStoreKitExtension
    {
        public static void InitializeEventStoreKitService( 
            this ContainerBuilder builder,
            Func<IComponentContext, EventStoreKitService> initializer = null )
        {
            // register default EventStoreConfiguration
            builder
                .RegisterInstance(
                    new EventStoreConfiguration
                    {
                        InsertBufferSize = 10000,
                        OnIddleInterval = 500
                    } )
                .As<IEventStoreConfiguration>()
                .IfNotRegistered( typeof(IEventStoreConfiguration) );

            // register service
            builder
                .Register( ctx =>
                {
                    var service = initializer.With( initialize => initialize( ctx ) ) ?? new EventStoreKitService();

                    ctx.ResolveOptional<IEventStoreConfiguration>().Do( config => service.SetConfiguration( config ) );

                    // ctx.ResolveKeyed<IDbProviderFactory>( new DataBaseConfiguration( connectionString ) );

                    // Register event handlers
                    var cmdHandlers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( registration => registration.Activator.LimitType.IsAssignableTo<ICommandHandler>() )
                        .Select( registration =>
                        {
                            var factoryGenerator = new FactoryGenerator( typeof( Func<ICommandHandler> ), registration, ParameterMapping.ByType );
                            return (Func<ICommandHandler>)factoryGenerator.GenerateFactory( ctx, new Parameter[] { } );
                        } )
                        .Where( h => h != null )
                        .ToList();
                    cmdHandlers.ForEach( handler => service.RegisterCommandHandler( handler ) );

                    // Register event subscribers
                    var subscribers = ctx
                        .ComponentRegistry
                        .Registrations
                        .Where( r => r.Activator.LimitType.IsAssignableTo<IEventSubscriber>() )
                        .Select( r =>
                            ctx.IsRegistered( r.Activator.LimitType ) ?
                            ctx.Resolve( r.Activator.LimitType ) :
                            r.Services.FirstOrDefault().With( ctx.ResolveService ) )
                        .Select( h => h.OfType<IEventSubscriber>() )
                        .Where( h => h != null )
                        .ToList();
                    //subscribers.ForEach( s => service.RegisterEventSubscriber(  ) );

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

        private static void SyncProcessCommand<TSubscriber,TEvent>( this IEventStoreKitService service, DomainCommand cmd )
            where TSubscriber: IEventSubscriber
            where TEvent : DomainEvent
        {
            var subscriber = service.GetSubscriber<TSubscriber>();
            var messageHandled = subscriber.CatchMessagesAsync<TEvent>( msg => msg.Id == cmd.Id );
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
            var builder = new ContainerBuilder();
            builder.RegisterModule<NorthwindModule>();
            builder.InitializeEventStoreKitService( ( ctx ) =>
            {
                return new EventStoreKitService()
                    .SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
                    .SetSubscriberDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
                    .RegisterEventSubscriber<ProductProjection>()
                    .RegisterEventSubscriber<CustomerProjection>()
                    .RegisterEventSubscriber<OrderProjection>()
                    .RegisterEventSubscriber<OrderDetailProjection>();
            } );

            var container = builder.Build();

            var service = container.Resolve<IEventStoreKitService>();
            service.CleanData();
            // ----------------------------------------------

            //var service = new EventStoreKitService()
            //    .RegisterCommandHandler()...
            //    .SetEventStoreDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
            //    .SetSubscriberDataBase<Linq2DbProviderFactory>( DbConnectionType.SqlLite, "data source=db1" )
            //    .RegisterEventSubscriber<ProductProjection>()
            //    .RegisterEventSubscriber<CustomerProjection>()
            //    .RegisterEventSubscriber<OrderProjection>()
            //    .RegisterEventSubscriber<OrderDetailProjection>();

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
