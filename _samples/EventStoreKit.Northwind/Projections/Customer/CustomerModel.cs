using System;
using LinqToDB.Mapping;

namespace EventStoreKit.Northwind.Projections.Customer
{
    [Table( "Customers", IsColumnAttributeRequired = false )]
    public class CustomerModel
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string CompanyName { get; set; }

        public string ContactName { get; set; }
        public string ContactTitle { get; set; }
        public string ContactPhone { get; set; }

        public string Address { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
    }
}