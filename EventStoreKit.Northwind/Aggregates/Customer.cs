using System;
using EventStoreKit.Aggregates;
using EventStoreKit.Northwind.Messages.Commands;
using EventStoreKit.Northwind.Messages.Events;
using EventStoreKit.Utility;

namespace EventStoreKit.Northwind.Aggregates
{
    public class Customer : TrackableAggregateBase
    {
        #region Private fields

        public string CompanyName;
        public string ContactName;
        public string ContactTitle;

        public string Address;
        public string City;
        public string Region;
        public string Country;
        public string PostalCode;
        public string ContactPhone;

        #endregion

        #region Event handlers

        private void Apply( CustomerCreatedEvent msg )
        {
            Id = msg.Id;
            
            CompanyName = msg.CompanyName;
            
            ContactName = msg.ContactName;
            ContactTitle = msg.ContactTitle;
            ContactPhone = msg.ContactPhone;

            Address = msg.Address;
            City = msg.City;
            Region = msg.Region;
            Country = msg.Country;
            PostalCode = msg.PostalCode;
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

        public Customer( Guid id )
        {
            Id = id;
            
            Register<CustomerCreatedEvent>( Apply );

            Register<CustomerRenamedEvent>( Apply );
            Register<CustomerContactChangedEvent>( Apply );
            Register<CustomerAddresChangedEvent>( Apply );
        }
        
        public Customer(CreateCustomerCommand cmd ) : this( cmd.Id )
        {
            IssuedBy = cmd.CreatedBy;
            RaiseEvent( cmd.CopyTo( c => new CustomerCreatedEvent() ) ); // its Ok if all fields equal in command and event
        }

        public void Update( UpdateCustomerCommand cmd )
        {
            if( CompanyName != cmd.CompanyName )
                RaiseEvent( new CustomerRenamedEvent { Id = Id, CompanyName = cmd.CompanyName } );

            if ( ContactName != cmd.ContactName ||
                 ContactTitle != cmd.ContactTitle ||
                 ContactPhone != cmd.ContactPhone )
            {
                RaiseEvent( cmd.CopyTo( c => new CustomerContactChangedEvent() ) );
            }

            if ( Address != cmd.Address ||
                 City != cmd.City ||
                 Region != cmd.Region ||
                 Country != cmd.Country ||
                 PostalCode != cmd.PostalCode )
            {
                RaiseEvent( cmd.CopyTo( c => new CustomerAddresChangedEvent() ) );
            }
        }
    }
}