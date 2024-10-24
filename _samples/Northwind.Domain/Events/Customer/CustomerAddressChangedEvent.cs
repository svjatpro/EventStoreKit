﻿namespace Northwind.Domain.Events.Customer
{
    public record CustomerAddressChangedEvent( Guid Id )
    {
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
    }
}
