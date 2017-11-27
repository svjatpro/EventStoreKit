using EventStoreKit.Handler;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;

namespace EventStoreKit.Northwind.AggregatesHandlers
{
    public class CustomerHandler :
        ICommandHandler<CreateCustomerCommand, Customer>,
        ICommandHandler<UpdateCustomerCommand, Customer>
    {
        public void Handle( CreateCustomerCommand cmd, CommandHandlerContext<Customer> context )
        {
            context.Entity = new Customer( cmd );
        }

        public void Handle( UpdateCustomerCommand cmd, CommandHandlerContext<Customer> context )
        {
            context.Entity.Update( cmd );
        }
    }
}