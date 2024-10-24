using EventStoreKit.Core;
using EventStoreKit.Core.Extensions;
using Northwind.Domain.Commands.Customer;
using Northwind.Domain.Events.Customer;

namespace Northwind.Domain.Aggregates
{
    public class Customer:
        ICommandHandler<CreateCustomerCommand>,
        ICommandHandler<UpdateCustomerCommand>
    {
        #region Private fields

        public Guid Id;
        public string? CompanyName;
        public string? ContactName;
        public string? ContactTitle;

        public string? Address;
        public string? City;
        public string? Region;
        public string? Country;
        public string? PostalCode;
        public string? ContactPhone;

        #endregion

        #region Event handlers

        public void Apply( CustomerCreatedEvent msg )
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

        public void Apply( CustomerRenamedEvent msg )
        {
            CompanyName = msg.CompanyName;
        }

        public void Apply( CustomerContactChangedEvent msg )
        {
            ContactName = msg.ContactName;
            ContactTitle = msg.ContactTitle;
            ContactPhone = msg.ContactPhone;
        }

        public void Apply( CustomerAddressChangedEvent msg )
        {
            Address = msg.Address;
            City = msg.City;
            Region = msg.Region;
            Country = msg.Country;
            PostalCode = msg.PostalCode;
        }

        #endregion

        public IEnumerable<object> Handle( CreateCustomerCommand cmd )
        {
            yield return cmd.CopyTo( c => new CustomerCreatedEvent{ Id = c.Id } );
        }

        public IEnumerable<object> Handle( UpdateCustomerCommand cmd )
        {
            if( CompanyName != cmd.CompanyName )
                yield return new CustomerRenamedEvent { Id = Id, CompanyName = cmd.CompanyName };

            if ( ContactName != cmd.ContactName ||
                 ContactTitle != cmd.ContactTitle ||
                 ContactPhone != cmd.ContactPhone )
            {
                yield return cmd.CopyTo( c => new CustomerContactChangedEvent( Id ) );
            }

            if ( Address != cmd.Address ||
                 City != cmd.City ||
                 Region != cmd.Region ||
                 Country != cmd.Country ||
                 PostalCode != cmd.PostalCode )
            {
                yield return cmd.CopyTo( c => new CustomerAddressChangedEvent( Id ) );
            }
        }
    }
}