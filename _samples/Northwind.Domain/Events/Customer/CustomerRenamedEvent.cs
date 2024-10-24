namespace Northwind.Domain.Events.Customer
{
    public record CustomerRenamedEvent
    {
        public required Guid Id { get; init; }
        public string? CompanyName { get; set; }
    }
}
