namespace Northwind.Domain.Commands.Product
{
    public class UpdateProductCommand
    {
        public required Guid Id { get; init; }
        public string? ProductName { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
