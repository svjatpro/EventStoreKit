namespace Northwind.Domain.Commands.Customer
{
    public record CreateCustomerCommand
    {
        public required Guid Id { get; init; }
        public string? CompanyName { get; set; }
        
        public string? ContactName { get; set; }
        public string? ContactTitle { get; set; }
        public string? ContactPhone { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
    }
}
