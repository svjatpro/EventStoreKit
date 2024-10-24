namespace Northwind.Domain.Commands.Order
{
    public class ShipOrderCommand
    {
        public required Guid OrderId { get; init; }
        public DateTime ShippedDate { get; set; }
    }
}
