using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStoreKit.Core.EventSubscribers;
using EventStoreKit.linq2db;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Northwind.Projections.Customer;
using EventStoreKit.Northwind.Projections.Order;
using EventStoreKit.Northwind.Projections.OrderDetail;
using EventStoreKit.Northwind.Projections.Product;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;
using EventStoreKit.Utility;

namespace Northwind.Embedded.Console
{
    public class NorthwindService
    {
        private readonly IEventStoreKitService EventStoreKitService;
        private readonly CustomerProjection CustomerProjection;
        private readonly ProductProjection ProductProjection;
        private readonly OrderProjection OrderProjection;
        private readonly OrderDetailProjection OrderDetailProjection;

        public NorthwindService( IDataBaseConfiguration dbConfiguration )
        {
            EventStoreKitService = new EventStoreKitService( false )
                .SetDataBase<Linq2DbProviderFactory>( dbConfiguration )
                .RegisterAggregate<Product>()
                .RegisterAggregate<Customer>()
                .RegisterAggregate<Order>()
                .RegisterAggregate<OrderDetail>()
                .RegisterEventSubscriber<ProductProjection>()
                .RegisterEventSubscriber<CustomerProjection>()
                .RegisterEventSubscriber<OrderProjection>()
                .RegisterEventSubscriber<OrderDetailProjection>()
                .Initialize();

            CustomerProjection = EventStoreKitService.GetSubscriber<CustomerProjection>();
            ProductProjection = EventStoreKitService.GetSubscriber<ProductProjection>();
            OrderProjection = EventStoreKitService.GetSubscriber<OrderProjection>();
            OrderDetailProjection = EventStoreKitService.GetSubscriber<OrderDetailProjection>();
        }

        public void CleanData()
        {
            EventStoreKitService.CleanData();
        }

        public List<CustomerModel> GetCustomers()
        {
            return CustomerProjection
                .GetCustomers( new SearchOptions( sorters: new List<SorterInfo>{ new SorterInfo{ FieldName = nameof( CustomerModel.CompanyName ) } } ) )
                .ToList();
        }
        public List<ProductModel> GetProducts()
        {
            return ProductProjection
                .GetProducts( new SearchOptions( sorters: new List<SorterInfo> { new SorterInfo { FieldName = nameof( ProductModel.ProductName ) } } ) )
                .ToList();
        }
        public List<OrderModel> GetOrders()
        {
            return OrderProjection
                .GetOrders( new SearchOptions( sorters: new List<SorterInfo> { new SorterInfo { FieldName = nameof( OrderModel.CustomerName ) } } ) )
                .ToList();
        }
        public List<OrderDetailModel> GetOrderDetails( Guid orderId )
        {
            return OrderDetailProjection
                .GetOrderDetails( orderId )
                .ToList();
        }

        public CustomerModel CreateCustomer(
            string companyName,
            string contactName, string contactTitle, string contactPhone,
            string address, string city, string region, string country, string postalCode )
        {
            var id = Guid.NewGuid();
            var task = CreateCustomerAsync( id, companyName, contactName, contactTitle, contactPhone, address, city, region, country, postalCode );
            task.Wait();
            return task.Result;
        }
        public Task<CustomerModel> CreateCustomerAsync(
            Guid id,
            string companyName,
            string contactName, string contactTitle, string contactPhone,
            string address, string city, string region, string country, string postalCode )
        {
            var waitTask = CustomerProjection.When<CustomerCreatedEvent>( msg => msg.Id == id );
            var resultTask = waitTask.ContinueWithEx( t =>
            {
                var customer = CustomerProjection.GetById( id );
                return customer;
            } );

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
            EventStoreKitService.SendCommand( cmd );
            
            return resultTask;
        }

        public ProductModel CreateProduct( string name, decimal price )
        {
            var id = Guid.NewGuid();
            var task = CreateProductAsync( id, name, price );
            task.Wait();
            return task.Result;
        }
        public Task<ProductModel> CreateProductAsync( Guid id, string name, decimal price )
        {
            var waitTask = ProductProjection.When<ProductCreatedEvent>( msg => msg.Id == id );
            var resultTask = waitTask.ContinueWithEx( t =>
            {
                var product = ProductProjection.GetById( id );
                return product;
            } );

            var cmd = new CreateProductCommand
            {
                Id = id,
                ProductName = name,
                UnitPrice = price
            };
            EventStoreKitService.SendCommand( cmd );

            return resultTask;
        }
        
        public OrderModel CreateOrder( Guid customerId, DateTime orderDate, DateTime requireDate )
        {
            var id = Guid.NewGuid();
            var task = CreateOrderAsync( id, customerId, orderDate, requireDate );
            task.Wait();
            return task.Result;
        }
        public Task<OrderModel> CreateOrderAsync( Guid id, Guid customerId, DateTime orderDate, DateTime requireDate )
        {
            var waitTask = OrderProjection.When<OrderCreatedEvent>( msg => msg.Id == id );
            var resultTask = waitTask.ContinueWithEx( t =>
            {
                var order = OrderProjection.GetById( id );
                return order;
            } );

            var cmd = new CreateOrderCommand
            {
                Id = id,
                CustomerId = customerId,
                OrderDate = orderDate,
                RequiredDate = requireDate
            };
            EventStoreKitService.SendCommand( cmd );

            return resultTask;
        }

        public OrderDetailModel CreateOrderDetail( Guid orderId, Guid productId, decimal unitPrice, decimal quantity, decimal discount = 0 )
        {
            var id = Guid.NewGuid();
            var task = CreateOrderDetailAsync( id, orderId, productId, unitPrice, quantity, discount );
            task.Wait();
            return task.Result;
        }
        public Task<OrderDetailModel> CreateOrderDetailAsync( Guid id, Guid orderId, Guid productId, decimal unitPrice, decimal quantity, decimal discount = 0 )
        {
            var waitTask = OrderDetailProjection.When<OrderDetailCreatedEvent>( msg => msg.Id == id );
            var resultTask = waitTask.ContinueWithEx( t =>
            {
                var detail = OrderDetailProjection.GetById( id );
                return detail;
            } );

            var cmd = new CreateOrderDetailCommand
            {
                Id = id,
                OrderId = orderId,
                ProductId = productId,
                UnitPrice = unitPrice,
                Quantity = quantity,
                Discount = discount
            };
            EventStoreKitService.SendCommand( cmd );

            return resultTask;
        }
    }
}
