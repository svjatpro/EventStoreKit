using System;
using EventStoreKit.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Utility;

namespace EventStoreKit.Northwind.Aggregates
{
    public class Product : TrackableAggregateBase
    {
        #region Private fields

        private string ProductName;
        private decimal UnitPrice;

        #endregion

        #region Event handlers

        private void Apply( ProductCreatedEvent msg )
        {
            Id = msg.Id;

            ProductName = msg.ProductName;
            UnitPrice = msg.UnitPrice;
        }

        private void Apply( CustomerRenamedEvent msg )
        {
            CompanyName = msg.CompanyName;
        }

        private void Apply( CustomerContactChangedEvent msg )
        {
            ContactName = msg.ContactName;
            ContactTitle = msg.ContactTitle;
            ContactPhone = msg.ContactPhone;
        }

        private void Apply( CustomerAddresChangedEvent msg )
        {
            Address = msg.Address;
            City = msg.City;
            Region = msg.Region;
            Country = msg.Country;
            PostalCode = msg.PostalCode;
        }

        #endregion

        public Product( Guid id )
        {
            Id = id;
            
            Register<ProductCreatedEvent>( Apply );

            Register<CustomerRenamedEvent>( Apply );
            Register<CustomerContactChangedEvent>( Apply );
            Register<CustomerAddresChangedEvent>( Apply );
        }
        
        public Product( CreateProductCommand cmd ) : this( cmd.Id )
        {
            IssuedBy = cmd.CreatedBy;
            RaiseEvent( cmd.CopyTo( c => new ProductCreatedEvent()) );
        }

        //public void Update( UpdateCustomerCommand cmd )
        //{
        //    if( CompanyName != cmd.CompanyName )
        //        RaiseEvent( new CustomerRenamedEvent { Id = Id, CompanyName = cmd.CompanyName } );

        //    if ( ContactName != cmd.ContactName ||
        //         ContactTitle != cmd.ContactTitle ||
        //         ContactPhone != cmd.ContactPhone )
        //    {
        //        RaiseEvent( cmd.CopyTo( c => new CustomerContactChangedEvent() ) );
        //    }

        //    if ( Address != cmd.Address ||
        //         City != cmd.City ||
        //         Region != cmd.Region ||
        //         Country != cmd.Country ||
        //         PostalCode != cmd.PostalCode )
        //    {
        //        RaiseEvent( cmd.CopyTo( c => new CustomerAddresChangedEvent() ) );
        //    }
        //}
        public void Update(UpdateProductCommand cmd)
        {
            
        }
    }
}