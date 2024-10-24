namespace Northwind.Domain.Events.Product;
public record ProductPriceUpdatedEvent( Guid Id, decimal UnitPrice );
