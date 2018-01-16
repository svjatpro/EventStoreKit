using System;
using LinqToDB.Mapping;

namespace OSMD.Common.ReadModels
{
    [Table( "OrderDetailsProducts", IsColumnAttributeRequired = false )]
    public class OrderDetailModelProduct
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string ProductName { get; set; }
 }
}