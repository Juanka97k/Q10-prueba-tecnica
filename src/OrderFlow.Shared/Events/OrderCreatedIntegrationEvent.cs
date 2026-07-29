namespace OrderFlow.Shared.Events;

public record OrderCreatedIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTime OcurridoEn
);
