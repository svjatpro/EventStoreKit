using System;
using EventStoreKit.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Utility;

namespace EventStoreKit.Northwind.Aggregates
{
    public class Product : TrackableAggregateBase
    {
        #region Private fields

        private string ProductName;
        private decimal UnitPrice;

        #endregion

        #region Event handlers

        private void Apply( ProductCreatedEvent msg )
        {
            Id = msg.Id;

            ProductName = msg.ProductName;
            UnitPrice = msg.UnitPrice;
        }

        private void Apply(ProductRenamedEvent msg )
        {
            ProductName = msg.ProductName;
        }

        private void Apply(ProductPriceUpdatedEvent msg )
        {
            UnitPrice = msg.UnitPrice;
        }

        #endregion

        public Product( Guid id )
        {
            Id = id;
            
            Register<ProductCreatedEvent>( Apply );
            Register<ProductRenamedEvent>( Apply );
            Register<ProductPriceUpdatedEvent>( Apply );
        }
        
        public Product( CreateProductCommand cmd ) : this( cmd.Id )
        {
            IssuedBy = cmd.CreatedBy;
            RaiseEvent( cmd.CopyTo( c => new ProductCreatedEvent()) );
        }

        public void Update(UpdateProductCommand cmd)
        {
            if( ProductName != cmd.ProductName )
                RaiseEvent( new ProductRenamedEvent
                {
                    Id = cmd.Id,
                    ProductName = cmd.ProductName
                } );
            if ( UnitPrice != cmd.UnitPrice )
            {
                RaiseEvent( new ProductPriceUpdatedEvent
                {
                    Id = cmd.Id,
                    UnitPrice = cmd.UnitPrice
                } );
            }
        }
    }
}