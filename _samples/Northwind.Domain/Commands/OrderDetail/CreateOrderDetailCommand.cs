namespace Northwind.Domain.Commands.OrderDetail
{
    public class CreateOrderDetailCommand
    {
        public required Guid Id { get; init; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Discount { get; set; }
    }
}