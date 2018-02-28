using System;
using CommonDomain.Core;
using EventStoreKit.Handler;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Utility;

namespace EventStoreKit.Northwind.Aggregates
{
    public class Order : AggregateBase,
        ICommandHandler<CreateOrderCommand>,
        ICommandHandler<ShippOrderCommand>
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

        public void Handle( CreateOrderCommand cmd )
        {
            RaiseEvent( cmd.CopyTo( c => new OrderCreatedEvent()) );
        }
        
        public void Handle( ShippOrderCommand cmd )
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