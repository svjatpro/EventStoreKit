using EventStoreKit.Handler;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;

namespace EventStoreKit.Northwind.AggregatesHandlers
{
    public class ProductHandler :
        ICommandHandler<CreateProductCommand, Product>
        //ICommandHandler<UpdateCustomerCommand, Customer>
    {
        public void Handle(CreateProductCommand cmd, CommandHandlerContext<Product> context )
        {
            context.Entity = new Product( cmd );
        }

        //public void Handle( UpdateCustomerCommand cmd, CommandHandlerContext<Customer> context )
        //{
        //    context.Entity.Update( cmd );
        //}
    }
}