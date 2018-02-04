﻿using EventStoreKit.Messages;

namespace EventStoreKit.Northwind.Messages.Events
{
    public class CustomerAddresChangedEvent : DomainEvent
    {
        public string Address { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
    }
}
