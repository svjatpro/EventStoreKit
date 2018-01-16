using System;
using LinqToDB.Mapping;

namespace OSMD.Common.ReadModels
{
    [Table( "OrderDetails", IsColumnAttributeRequired = false )]
    public class OrderDetailModel
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }

        public Guid ProductId { get; set; }
        public string ProductName { get; set; }

        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Discount { get; set; }
    }
}