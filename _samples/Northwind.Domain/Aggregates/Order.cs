using EventStoreKit.Core;
using EventStoreKit.Core.Extensions;
using Northwind.Domain.Commands.Order;
using Northwind.Domain.Events.Order;

namespace Northwind.Domain.Aggregates
{
    public class Order :
        ICommandHandler<CreateOrderCommand>,
        ICommandHandler<ShipOrderCommand>
    {
        #region Private fields

        private Guid Id;
        private Guid CustomerId;
        private DateTime OrderDate;
        private DateTime RequiredDate;
        private DateTime? ShippedDate;

        #endregion

        #region Event handlers

        public void Apply( OrderCreatedEvent msg )
        {
            Id = msg.Id;

            CustomerId = msg.CustomerId;
            OrderDate = msg.OrderDate;
            RequiredDate = msg.RequiredDate;
        }

        public void Apply( OrderShippedEvent msg )
        {
            ShippedDate = msg.ShippedDate;
        }

        #endregion

        public IEnumerable<object> Handle( CreateOrderCommand cmd )
        {
            yield return cmd.CopyTo( c => new OrderCreatedEvent( Id ) );
        }
        
        public IEnumerable<object> Handle( ShipOrderCommand cmd )
        {
            yield return new OrderShippedEvent { Id = Id, ShippedDate = cmd.ShippedDate };
        }
    }
}