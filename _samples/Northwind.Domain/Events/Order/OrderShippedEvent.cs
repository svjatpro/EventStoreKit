namespace Northwind.Domain.Events.Order
{
    public record OrderShippedEvent
    {
        public required Guid Id { get; init; }
        public DateTime ShippedDate { get; set; }
    }
}
