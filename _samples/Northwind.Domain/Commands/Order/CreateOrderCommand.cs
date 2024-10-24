namespace Northwind.Domain.Commands.Order
{
    public class CreateOrderCommand
    {
        public required Guid Id { get; init; }
        public Guid CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime RequiredDate { get; set; }
    }
}
