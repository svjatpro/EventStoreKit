using System;
using LinqToDB.Mapping;

namespace EventStoreKit.Northwind.Projections.Order
{
    [Table( "OrderCustomer", IsColumnAttributeRequired = false )]
    public class OrderModelCustomer
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string CompanyName { get; set; }
    }
}