namespace Northwind.Domain.Events.OrderDetail
{
    public record OrderDetailCreatedEvent( Guid Id )
    {
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal Discount { get; set; }
    }
}
