using EventStoreKit.Handler;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;

namespace EventStoreKit.Northwind.AggregatesHandlers
{
    public class ProductHandler :
        ICommandHandler<CreateProductCommand, Product>,
        ICommandHandler<UpdateProductCommand, Product>
    {
        public void Handle(CreateProductCommand cmd, CommandHandlerContext<Product> context )
        {
            context.Entity = new Product( cmd );
        }
        
        public void Handle( UpdateProductCommand cmd, CommandHandlerContext<Product> context )
        {
            context.Entity.Update( cmd );
        }
    }
}