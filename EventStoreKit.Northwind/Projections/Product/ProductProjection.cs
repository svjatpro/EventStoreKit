using System;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Northwind.Projections.Product;
using EventStoreKit.Projections;
using EventStoreKit.SearchOptions;
using EventStoreKit.Services;

namespace EventStoreKit.Northwind.Projections.Customer
{
    public class ProductProjection : SqlProjectionBase<ProductModel>,
        IEventHandler<ProductCreatedEvent>
    {
        #region Event handlers

        public void Handle(ProductCreatedEvent msg)
        {
            DbProviderFactory.Run( db => db.Insert( new ProductModel
            {
                Id = msg.Id,
                ProductName = msg.ProductName,
                UnitPrice = msg.UnitPrice
            } ) );
        }

        #endregion

        public ProductProjection( IEventStoreSubscriberContext context ) : base( context )
        {
        }

        public string Name => "Products Projection";

        public ProductModel GetById( Guid id )
        {
            return DbProviderFactory.Run( db => db.Single<ProductModel>( c => c.Id == id ) );
        }
        public QueryResult<ProductModel> GetProducts( SearchOptions.SearchOptions options )
        {
            return Search( options );
        }
       
    }
}
