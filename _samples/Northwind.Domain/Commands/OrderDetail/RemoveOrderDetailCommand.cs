namespace Northwind.Domain.Commands.OrderDetail
{
    public class RemoveOrderDetailCommand
    {
        public required Guid Id { get; init; }
        public Guid OrderId { get; set; }
    }
}
