using EventStoreKit.Core;
using EventStoreKit.Core.Extensions;
using Northwind.Domain.Commands.OrderDetail;
using Northwind.Domain.Events.OrderDetail;

namespace Northwind.Domain.Aggregates
{
    public class OrderDetail :
        ICommandHandler<CreateOrderDetailCommand>,
        ICommandHandler<RemoveOrderDetailCommand>
    {
        #region private fields

        private Guid Id;
        private Guid OrderId;
        private Guid ProductId;
        private decimal UnitPrice;
        private decimal Quantity;
        private decimal Discount;

        private bool Removed;

        #endregion

        #region Event handlers

        public void Apply( OrderDetailCreatedEvent msg )
        {
            Id = msg.Id;
            OrderId = msg.OrderId;
            ProductId = msg.ProductId;
            UnitPrice = msg.UnitPrice;
            Quantity = msg.Quantity;
            Discount = msg.Discount;
        }

        public void Apply( OrderDetailRemovedEvent msg )
        {
            Removed = true;
        }

        #endregion

        public IEnumerable<object> Handle( CreateOrderDetailCommand cmd )
        {
            yield return cmd.CopyTo( c => new OrderDetailCreatedEvent( Id ) );
        }

        public IEnumerable<object> Handle( RemoveOrderDetailCommand cmd )
        {
            yield return new OrderDetailRemovedEvent( cmd.Id, cmd.OrderId );
        }
    }
}
