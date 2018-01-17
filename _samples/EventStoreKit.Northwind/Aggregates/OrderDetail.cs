using System;
using EventStoreKit.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Utility;

namespace EventStoreKit.Northwind.Aggregates
{
    public class OrderDetail : TrackableAggregateBase
    {
        #region private fields

        private Guid OrderId;
        private Guid ProductId;
        private decimal UnitPrice;
        private decimal Quantity;
        private decimal Discount;

        private bool Removed;

        #endregion

        #region Event handlers

        private void Apply( OrderDetailCreatedEvent msg )
        {
            OrderId = msg.OrderId;
            ProductId = msg.ProductId;
            UnitPrice = msg.UnitPrice;
            Quantity = msg.Quantity;
            Discount = msg.Discount;
        }

        private void Apply( OrderDetailRemovedEvent msg )
        {
            Removed = true;
        }

        #endregion

        public OrderDetail( Guid id )
        {
            Id = id;

            Register<OrderDetailCreatedEvent>( Apply );
            Register<OrderDetailRemovedEvent>( Apply );
        }

        public OrderDetail( CreateOrderDetailCommand cmd ) : this( cmd.Id )
        {
            RaiseEvent( cmd.CopyTo( c => new OrderDetailCreatedEvent() ) );
        }

        public void Remove( RemoveOrderDetailCommand cmd )
        {
            RaiseEvent( cmd.CopyTo( c => new OrderDetailRemovedEvent() ) );
        }
    }
}
