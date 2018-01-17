using System;
using LinqToDB.Mapping;

namespace EventStoreKit.Northwind.Projections.Order
{
    [Table( "Orders", IsColumnAttributeRequired = false )]
    public class OrderModel
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public DateTime OrderDate { get; set; }
        public DateTime RequiredDate { get; set; }

        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
    }
}