using System;
using LinqToDB.Mapping;

namespace EventStoreKit.Northwind.Projections.OrderDetail
{
    [Table( "OrderDetailsProducts", IsColumnAttributeRequired = false )]
    public class OrderDetailModelProduct
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string ProductName { get; set; }
 }
}