namespace Northwind.Domain.Events.Product
{
    public record ProductCreatedEvent( Guid Id )
    {
        public string? ProductName { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
