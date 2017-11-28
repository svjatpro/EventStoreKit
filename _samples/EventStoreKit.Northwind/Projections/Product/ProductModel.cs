using System;
using LinqToDB.Mapping;

namespace OSMD.Common.ReadModels
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