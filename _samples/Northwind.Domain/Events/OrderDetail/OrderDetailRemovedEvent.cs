namespace Northwind.Domain.Events.OrderDetail;
public record OrderDetailRemovedEvent( Guid Id, Guid OrderId );