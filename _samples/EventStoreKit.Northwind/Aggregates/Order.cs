using System;
using EventStoreKit.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Utility;

namespace EventStoreKit.Northwind.Aggregates
{
    public class Order : TrackableAggregateBase
    {
        #region Private fields

        private Guid CustomerId;
        private DateTime OrderDate;
        private DateTime RequiredDate;
        private DateTime? ShippedDate;

        #endregion

        #region Event handlers

        private void Apply( OrderCreatedEvent msg )
        {
            Id = msg.Id;

            CustomerId = msg.CustomerId;
            OrderDate = msg.OrderDate;
            RequiredDate = msg.RequiredDate;
        }

        private void Apply( OrderShippedEvent msg )
        {
            ShippedDate = msg.ShippedDate;
        }

        #endregion

        public Order( Guid id )
        {
            Id = id;
            
            Register<OrderCreatedEvent>( Apply );
            Register<OrderShippedEvent>( Apply );
        }

        public Order( CreateOrderCommand cmd ) : this( cmd.Id )
        {
            IssuedBy = cmd.CreatedBy;
            RaiseEvent( cmd.CopyTo( c => new OrderCreatedEvent()) );
        }
        
        public void Shipp( ShippOrderCommand cmd )
        {
            RaiseEvent( 
                new OrderShippedEvent
                {
                    Id = Id,
                    ShippedDate = cmd.ShippedDate
                } );
        }
    }
}