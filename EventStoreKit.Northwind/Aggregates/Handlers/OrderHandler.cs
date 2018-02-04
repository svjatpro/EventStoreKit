using EventStoreKit.Handler;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;

namespace EventStoreKit.Northwind.AggregatesHandlers
{
    public class OrderHandler :
        ICommandHandler<CreateOrderCommand, Order>,
        ICommandHandler<ShippOrderCommand, Order>
    {
        public void Handle(CreateOrderCommand cmd, CommandHandlerContext<Order> context )
        {
            context.Entity = new Order( cmd );
        }

        public void Handle( ShippOrderCommand cmd, CommandHandlerContext<Order> context )
        {
            context.Entity.Shipp( cmd );
        }
    }
}