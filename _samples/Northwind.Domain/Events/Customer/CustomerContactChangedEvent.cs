namespace Northwind.Domain.Events.Customer
{
    public record CustomerContactChangedEvent(Guid Id)
    {
        public string? ContactName { get; set; }
        public string? ContactTitle { get; set; }
        public string? ContactPhone { get; set; }
    }
}
