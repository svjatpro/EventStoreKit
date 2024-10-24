using EventStoreKit.Core;
using EventStoreKit.Core.Extensions;
using Northwind.Domain.Commands.Product;
using Northwind.Domain.Events.Product;

namespace Northwind.Domain.Aggregates
{
    public class Product :
        ICommandHandler<CreateProductCommand>,
        ICommandHandler<UpdateProductCommand>
    {
        #region Private fields

        private Guid Id;
        private string? ProductName;
        private decimal UnitPrice;

        #endregion

        #region Event handlers

        public void Apply( ProductCreatedEvent msg )
        {
            Id = msg.Id;

            ProductName = msg.ProductName;
            UnitPrice = msg.UnitPrice;
        }

        public void Apply(ProductRenamedEvent msg )
        {
            ProductName = msg.ProductName;
        }

        public void Apply(ProductPriceUpdatedEvent msg )
        {
            UnitPrice = msg.UnitPrice;
        }

        #endregion

        public IEnumerable<object> Handle( CreateProductCommand cmd )
        {
            yield return cmd.CopyTo( c => new ProductCreatedEvent( Id ) );
        }

        public IEnumerable<object> Handle( UpdateProductCommand cmd )
        {
            if( cmd.ProductName != null && ProductName != cmd.ProductName ) 
                yield return new ProductRenamedEvent( cmd.Id, cmd.ProductName );
            if ( UnitPrice != cmd.UnitPrice )
                yield return new ProductPriceUpdatedEvent( cmd.Id, cmd.UnitPrice );
        }
    }
}