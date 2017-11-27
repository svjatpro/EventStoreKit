using EventStoreKit.Handler;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;

namespace EventStoreKit.Northwind.AggregatesHandlers
{
    public class CustomerHandler :
        ICommandHandler<CreateProductCommand, Customer>,
        ICommandHandler<UpdateCustomerCommand, Customer>
    {
        public void Handle( CreateProductCommand cmd, CommandHandlerContext<Customer> context )
        {
            context.Entity = new Customer( cmd );
        }

        public void Handle( UpdateCustomerCommand cmd, CommandHandlerContext<Customer> context )
        {
            context.Entity.Update( cmd );
        }
    }
}