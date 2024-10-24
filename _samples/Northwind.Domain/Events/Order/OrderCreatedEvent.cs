namespace Northwind.Domain.Events.Order
{
    public record OrderCreatedEvent( Guid Id )
    {
        public Guid CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime RequiredDate { get; set; }
    }
}
