using System;
using System.Linq;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Projections;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;
using OSMD.Common.ReadModels;

namespace EventStoreKit.Northwind.Projections.Customer
{
    public class OrderProjection : SqlProjectionBase<OrderModel>,
        IEventHandler<OrderCreatedEvent>,
        IEventHandler<CustomerCreatedEvent>,
        IEventHandler<CustomerRenamedEvent>
    {
        #region Event handlers

        public void Handle(OrderCreatedEvent msg)
        {
            DbProviderFactory.Run( db =>
            {
                db.Insert( new OrderModel
                {
                    Id = msg.Id,
                    OrderDate = msg.OrderDate,
                    RequiredDate = msg.RequiredDate,
                    CustomerId = msg.CustomerId,
                    CustomerName = db.Single<OrderModelCustomer>( c => c.Id == msg.CustomerId ).CompanyName
                } );
            } );
        }

        public void Handle( CustomerCreatedEvent msg )
        {
            DbProviderFactory.Run( db =>
                db.Insert( new OrderModelCustomer
                {
                    Id = msg.Id,
                    CompanyName = msg.CompanyName
                } ) );
        }

        public void Handle( CustomerRenamedEvent msg )
        {
            DbProviderFactory.Run( db =>
            {
                db.Update<OrderModelCustomer>(
                    c => c.Id == msg.Id,
                    c => new OrderModelCustomer {  CompanyName = msg.CompanyName } );
                db.Update<OrderModel>(
                    c => c.CustomerId == msg.Id,
                    c => new OrderModel { CustomerName = msg.CompanyName } );
            } );
        }

        #endregion

        public OrderProjection( IEventStoreSubscriberContext context ) : base( context )
        {
            RegisterReadModel<OrderModelCustomer>();
        }

        public string Name => "Orders Projection";

        public OrderModel GetById( Guid id )
        {
            return DbProviderFactory.Run( db => db.Single<OrderModel>( c => c.Id == id ) );
        }

        public QueryResult<OrderModel> GetOrders( SearchOptions.SearchOptions options )
        {
            return Search( options );
        }
    }
}
