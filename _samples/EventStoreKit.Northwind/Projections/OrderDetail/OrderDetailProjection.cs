using System;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Projections;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;
using EventStoreKit.Utility;

namespace EventStoreKit.Northwind.Projections.OrderDetail
{
    public class OrderDetailProjection : SqlProjectionBase<OrderDetailModel>,
        IEventHandler<OrderDetailCreatedEvent>,
        IEventHandler<OrderDetailRemovedEvent>,
        IEventHandler<ProductCreatedEvent>,
        IEventHandler<ProductRenamedEvent>
    {
        #region Event handlers

        public void Handle(OrderDetailCreatedEvent msg)
        {
            var prod = DbProviderFactory.Run( db => db.SingleOrDefault<OrderDetailModelProduct>( c => c.Id == msg.ProductId ).With( p => p.ProductName ) );
            DbProviderFactory.Run( db =>
            {
                db.Insert( new OrderDetailModel
                {
                    Id = msg.Id,
                    OrderId = msg.OrderId,
                    ProductId = msg.ProductId,
                    //ProductName = db.Single<OrderDetailModelProduct>( c => c.Id == msg.ProductId ).ProductName,
                    ProductName = prod ?? string.Empty,
                    UnitPrice = msg.UnitPrice,
                    Quantity = msg.Quantity,
                    Discount = msg.Discount
                } );
            } );
        }

        public void Handle( OrderDetailRemovedEvent msg )
        {
            DbProviderFactory.Run( db => db.Delete<OrderDetailModel>( detail => detail.Id == msg.Id ) );
        }


        public void Handle( ProductCreatedEvent msg )
        {
            DbProviderFactory.Run( db =>
                db.Insert( new OrderDetailModelProduct
                {
                    Id = msg.Id,
                    ProductName = msg.ProductName
                } ) );
        }

        public void Handle( ProductRenamedEvent msg )
        {
            DbProviderFactory.Run( db =>
            {
                db.Update<OrderDetailModelProduct>(
                    c => c.Id == msg.Id,
                    c => new OrderDetailModelProduct { ProductName = msg.ProductName } );
                db.Update<OrderDetailModel>(
                    c => c.ProductId == msg.Id,
                    c => new OrderDetailModel { ProductName = msg.ProductName } );
            } );
        }

        #endregion

        public OrderDetailProjection( IEventStoreSubscriberContext context ) : base( context )
        {
            RegisterReadModel<OrderDetailModelProduct>();
        }

        public string Name => "Orders Projection";

        public OrderDetailModel GetById( Guid id )
        {
            return DbProviderFactory.Run( db => db.Single<OrderDetailModel>( c => c.Id == id ) );
        }

        public QueryResult<OrderDetailModel> GetOrderDetails( Guid orderId, SearchOptions.SearchOptions options = null )
        {
            options
                .EnsureFilterAtStart<OrderDetailModel>( 
                    detail => detail.OrderId, 
                    filter => { filter.Value = orderId; } );
            return Search( options );
        }
    }
}
