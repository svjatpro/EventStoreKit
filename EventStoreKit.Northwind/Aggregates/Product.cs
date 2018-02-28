using System;
using CommonDomain.Core;
using EventStoreKit.Handler;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Utility;

namespace EventStoreKit.Northwind.Aggregates
{
    public class Product : AggregateBase,
        ICommandHandler<CreateProductCommand>,
        ICommandHandler<UpdateProductCommand>
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
        
        public void Handle( CreateProductCommand cmd )
        {
            RaiseEvent( cmd.CopyTo( c => new ProductCreatedEvent()) );
        }

        public void Handle( UpdateProductCommand cmd )
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