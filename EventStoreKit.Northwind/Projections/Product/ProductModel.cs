using System;
using LinqToDB.Mapping;

namespace EventStoreKit.Northwind.Projections.Product
{
    [Table( "Products", IsColumnAttributeRequired = false )]
    public class ProductModel
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
    }
}