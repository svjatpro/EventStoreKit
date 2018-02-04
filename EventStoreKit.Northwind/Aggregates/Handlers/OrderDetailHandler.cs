using EventStoreKit.Handler;
using EventStoreKit.Northwind.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;

namespace EventStoreKit.Northwind.AggregatesHandlers
{
    public class OrderDetailHandler :
        ICommandHandler<CreateOrderDetailCommand, OrderDetail>,
        ICommandHandler<RemoveOrderDetailCommand, OrderDetail>
    {
        public void Handle( CreateOrderDetailCommand cmd, CommandHandlerContext<OrderDetail> context )
        {
            context.Entity = new OrderDetail( cmd );
        }

        public void Handle( RemoveOrderDetailCommand cmd, CommandHandlerContext<OrderDetail> context )
        {
            context.Entity.Remove( cmd );
        }
    }
}